using Fclp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hyddan.Test.Load
{
    class Load
    {
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 99999;
            var parser = new FluentCommandLineBuilder<Configuration>();

            parser.Setup<string>(arg => arg.Condition)
                    .As('c', "condition");

            parser.Setup<string>(arg => arg.Data)
                    .As('d', "data");

            parser.Setup<int>(arg => arg.Executions)
                    .As('e', "executions");

            parser.Setup<string>(arg => arg.File)
                    .As('f', "file");

            parser.Setup<List<string>>(arg => arg.Headers)
                    .As('h', "headers");

            parser.Setup<string>(arg => arg.Method)
                    .As('m', "method");

            parser.Setup<int>(arg => arg.Requests)
                    .As('r', "requests");

            parser.Setup<int>(arg => arg.Timeout)
                    .As('t', "timeout");

            parser.Setup<string>(arg => arg.Url)
                    .As('u', "url");

            var result = parser.Parse(args);

            if (!result.HasErrors)
            {
                var configuration = parser.Object;

                if (!string.IsNullOrEmpty(parser.Object.File))
                {
                    using (var reader = new StreamReader(File.OpenRead(parser.Object.File)))
                    {
                        configuration = ServiceStack.Text.JsonSerializer.DeserializeFromString<Configuration>(reader.ReadToEnd());
                        configuration.Condition = parser.Object.Condition ?? configuration.Condition ?? string.Empty;
                        configuration.Data = parser.Object.Data ?? configuration.Data ?? string.Empty;
                        configuration.Executions = 0 != parser.Object.Executions ? parser.Object.Executions : 0 != configuration.Executions ? configuration.Executions : 1;
                        configuration.Headers = parser.Object.Headers ?? configuration.Headers ?? new List<string>();
                        configuration.Method = parser.Object.Method ?? configuration.Method ?? "POST";
                        configuration.Requests = 0 != parser.Object.Requests ? parser.Object.Requests : 0 != configuration.Requests ? configuration.Requests : 1000;
                        configuration.Timeout = 0 != parser.Object.Timeout ? parser.Object.Timeout : 0 != configuration.Timeout ? configuration.Timeout : 60000;
                        configuration.Url = parser.Object.Url ?? configuration.Url ?? "http://127.0.0.1/";
                    }
                }

                int executionCount = 0;
                do
                {
                    DoWork(configuration);
                } while (configuration.Executions > ++executionCount);
            }
        }

        public static void DoWork(Configuration configuration)
        {
            int failed = 0, requests = 0;
            long averageResponseTime = 0;
            var requestTimes = new System.Collections.Concurrent.ConcurrentBag<long>();

            var totalRequestTimer = new System.Diagnostics.Stopwatch();
            var totalResponseTimer = new System.Diagnostics.Stopwatch();

            var tasks = new System.Collections.Concurrent.ConcurrentStack<Task>();
            Parallel.For(0, configuration.Requests, new ParallelOptions() { MaxDegreeOfParallelism = int.MaxValue }, (i) =>
            {
                var task = Task.Factory.StartNew(async () =>
                {
                    Interlocked.Increment(ref requests);
                    if (requests == configuration.Requests)
                    {
                        totalRequestTimer.Stop();
                    }

                    var singleRequestTimer = new System.Diagnostics.Stopwatch();
                    singleRequestTimer.Start();
                    var result = await DoRequestAsync(configuration);
                    singleRequestTimer.Stop();
                    var time = (long)singleRequestTimer.Elapsed.TotalMilliseconds;

                    if (-1 != result)
                    {
                        requestTimes.Add(time);
                        Interlocked.Add(ref averageResponseTime, time);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                    }
                });

                tasks.Push(task);
            });

            totalRequestTimer.Start();
            totalResponseTimer.Start();

            Task.WaitAll(tasks.Where(t => null != t).ToArray());
            totalResponseTimer.Stop();

            var counter = 0;
            foreach (var time in requestTimes)
            {
                ++counter;
                if (20000 < time)
                {
                    Console.Write("{0}__{1}, ", counter, time);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Requests performed:\t\t\t\t{0}", requests);
            Console.WriteLine("Failed requests:\t\t\t\t{0}", failed);
            Console.WriteLine("Time taken to perform requests (ms):\t\t{0}", totalRequestTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Time taken to receive responses (ms):\t\t{0}", totalResponseTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Average response time per request (ms):\t\t{0}", averageResponseTime / requests);
            Console.WriteLine("Longest response time (ms):\t\t\t{0}", requestTimes.Any() ? requestTimes.Max() : 0);
        }

        public static Task<long> DoRequestAsync(Configuration configuration)
        {
            var data = Encoding.UTF8.GetBytes(configuration.Data);

            var request = (HttpWebRequest)WebRequest.Create(configuration.Url);
            request.Method = configuration.Method;
            request.Timeout = request.ReadWriteTimeout = configuration.Timeout;
            request.ContentLength = data.Length;

            foreach (var header in configuration.Headers)
            {
                var headerItems = header.Split(':');
                switch (headerItems[0].ToLower())
                {
                    case "content-type":
                        request.ContentType = headerItems[1];
                        break;
                    case "host":
                        request.Host = headerItems[1];
                        break;
                    case "user-agent":
                        request.UserAgent = headerItems[1];
                        break;
                    default:
                        request.Headers.Add(headerItems[0], headerItems[1]);
                        break;
                }
            }

            var result = new TaskCompletionSource<long>();
            if ("GET".Equals(configuration.Method, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    DoGetResponseAsync(request, configuration, result);
                }
                catch (Exception)
                {
                    result.SetResult(-1);
                }

                return result.Task;
            }
            else
            {
                Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, request)
                   .ContinueWith(requestStreamTask =>
                   {
                       try
                       {
                           using (var localStream = requestStreamTask.Result)
                           {
                               localStream.Write(data, 0, data.Length);
                               localStream.Flush();
                           }

                           DoGetResponseAsync(request, configuration, result);
                       }
                       catch (Exception)
                       {
                           result.SetResult(-1);
                       }

                   }, TaskContinuationOptions.AttachedToParent);

                return result.Task;
            }
        }

        public static void DoGetResponseAsync(HttpWebRequest request, Configuration configuration, TaskCompletionSource<long> result)
        {
            var timer = (Timer) null;
            Task.Factory.FromAsync<WebResponse>((callback, obj) =>
            {
                timer = new Timer((state) =>
                {
                    request.Abort();
                    timer.Dispose();
                }, new object(), configuration.Timeout, Timeout.Infinite);
                
                return request.BeginGetResponse(callback, obj);
            }, request.EndGetResponse, request)
                .ContinueWith(responseTask =>
                {
                    try
                    {
                        using (var webResponse = responseTask.Result)
                        using (var responseStream = webResponse.GetResponseStream())
                        using (var sr = new StreamReader(responseStream, Encoding.UTF8))
                        {
                            var responseString = sr.ReadToEnd();
                            timer.Dispose();

                            if (!responseString.Contains(configuration.Condition))
                            {
                                throw new Exception();
                            }

                            result.SetResult(1);
                        }
                    }
                    catch (Exception)
                    {
                        result.SetResult(-1);
                    }
                }, TaskContinuationOptions.AttachedToParent);
        }
    }
}