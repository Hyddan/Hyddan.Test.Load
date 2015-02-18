# Hyddan.Test.Load
A high performance load generation tool written in C#, based on TPL and asynchronous WebRequests.

This tool was developed due to the lack of existing reliable and configurable free alternatives. Its main objective is to momentaneously measure how many concurrent requests a service can handle. To achieve this, the tool will give the time it took to send the specified number of requests. For example, if 1000 requests were sent, it took exactly 1 second to send them and none of the requests failed the service could be considered to be able to handle 1000 requests/second.

As of version 1.0.0.0 it is also possible to set the tool run a number of times consecutively in order to produce continuous load.

## Compilation
The project produced two main output files; Hyddan.Test.Load.exe and Hyddan.Test.Load.NET451.exe.

### Hyddan.Test.Load.exe
The original executable produced from the project. In order to run this the following required binaries must be in the same folder as the executable:
* FluentCommandLineParser.dll
* ServiceStack.Text.dll
* System.Net.Http.Formatting.dll

### Hyddan.Test.Load.NET451.exe
An additional  executable produced from the project with the required binaries included (merged using ILMerge.exe).

## Usage
```
Hyddan.Test.Load.exe [switch ...]
```

## Configuration
```
Option                Description                                           Type            Default Value
--------------------------------------------------------------------------------------------------------------
-c, --condition       Condition of satisfaction. If specified this must     string          string.Empty
                      exist in the body of the response for the request
                      to be considered successful.

-d, --data            Data to send in the body of the requests.             string          string.Empty

-e, --executions      Number of times to run the tool consecutively.        int             1

-f, --file            JSON file specifying base configurations. All         string          string.Empty
                      other switches override values provided in this
                      file.

-h, --headers         Space-separated list of headers to be sent with       List<string>    new List<string>()
                      the requests. Each header is specified in the
                      format HeaderName:HeaderValue (enclosed in quotes
                      if they contain spaces)

-m, --method          HTTP method to use for the requests.                  string          POST

-r, --requests        Number of requests to send.                           int             1000

-t, --timeout         Timeout for the requests in milliseconds.             int             60000

-u, --url             Url to use for the requests.                          string          http://127.0.0.1/
```

## How to interpret the output statistics
Any successful request taking longer than 20000 miliseconds will be printed in the format [RequestNumber]__[ResponseTime]. Following that a report is printed with the values:
```
Metric                                    Description                               Type            Unit Of Measurement
-------------------------------------------------------------------------------------------------------------------------
Requests performed                        Number of requests sent.                  int             Count
Failed requests                           Number of failed requests.                int             Count
Time taken to perform requests (ms)       Time it took to send the requests.        double          Milliseconds
Time taken to receive responses (ms)      Time it took to receive the responses.    double          Milliseconds
Average response time per request (ms)    Average response time for the requests.   long            Milliseconds/Requests
Longest response time (ms)                Longest response time of the requests.    long            Milliseconds
```

## Known limitations
The Condition of satisfaction should to be improved to allow choosing acceptable and non-acceptable response codes as well as more complex match criterias checking against both the response body and headers.
