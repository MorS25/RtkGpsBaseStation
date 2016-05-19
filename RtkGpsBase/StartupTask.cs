using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    public sealed class StartupTask : IBackgroundTask{
        private static BackgroundTaskDeferral _deferral;
        private HttpServer _httpServer;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            _httpServer = new HttpServer(8181);
            _httpServer.StartServer();
        }
    }

    internal sealed class HttpServer : IDisposable{
        public delegate void HttpRequestReceivedEvent(HttpRequest request);

        private readonly StreamSocketListener _listener;
        private readonly int _port;

        private readonly SerialPort _serialPort;

        public HttpServer(int serverPort)
        {
            _serialPort = new SerialPort("AI041RYG", 57600, 5000, 5000);

            if (_listener == null)
            {
                _listener = new StreamSocketListener();
            }
            _port = serverPort;

            _listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        public event HttpRequestReceivedEvent OnRequestReceived;

        internal async void StartServer()
        {
            try
            {
                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Http Server could not bind to service {0}. Error {1}", _port, ex.Message);
            }
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            HttpRequest request;
            using (var stream = socket.InputStream)
            {
                var parser = new HttpRequestParser();
                request = await parser.GetHttpRequestForStream(stream);
                OnRequestReceived?.Invoke(request);
            }

            using (var output = socket.OutputStream)
            {
                try
                {
                    if (request.Method == "GET")
                    {
                        WriteResponse(output);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private void WriteResponse(IOutputStream output)
        {
            var resp = output.AsStreamForWrite();

            while (true) //Stream whatever we get from the base station GPS to the client
            {
                try
                {
                    var bodyArray = _serialPort.Read();

                    var stream = new MemoryStream(bodyArray);
                    resp.WriteAsync(bodyArray, 0, bodyArray.Length);
                    stream.CopyToAsync(resp);
                    resp.FlushAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    break;
                }
            }
        }
    }

    internal enum HttpParserState{
        METHOD,
        URL,
        URLPARM,
        URLVALUE,
        VERSION,
        HEADERKEY,
        HEADERVALUE,
        BODY,
        OK
    };

    public enum HttpResponseState{
        OK = 200,
        BAD_REQUEST = 400,
        NOT_FOUND = 404
    }

    public sealed class HttpRequest{
        public string Method { get; set; }

        public string URL { get; set; }

        public string Version { get; set; }

        public IEnumerable<KeyValuePair<string, string>> UrlParameters { get; set; }

        public bool Execute { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public int BodySize { get; set; }

        public string BodyContent { get; set; }
    }

    public sealed class HttpRequestParser{
        private readonly uint BufferSize = 8192;

        private readonly HttpRequest HTTPRequest;
        private HttpParserState ParserState;

        public HttpRequestParser(uint readBuffer)
        {
            HTTPRequest = new HttpRequest();
            BufferSize = readBuffer;
        }

        public HttpRequestParser() :
            this(8192)
        {
        }

        public IAsyncOperation<HttpRequest> GetHttpRequestForStream(IInputStream stream)
        {
            return ProcessStream(stream).AsAsyncOperation();
        }

        private async Task<HttpRequest> ProcessStream(IInputStream stream)
        {
            Dictionary<string, string> _httpHeaders = null;
            Dictionary<string, string> _urlParameters = null;

            var data = new byte[BufferSize];
            var requestString = new StringBuilder();
            var dataRead = BufferSize;

            var buffer = data.AsBuffer();

            var hValue = "";
            var hKey = "";

            try
            {
                // binary data buffer index
                uint bfndx = 0;

                // Incoming message may be larger than the buffer size.
                while (dataRead == BufferSize)
                {
                    await stream.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                    requestString.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;

                    // read buffer index
                    uint ndx = 0;
                    do
                    {
                        switch (ParserState)
                        {
                            case HttpParserState.METHOD:
                                if (data[ndx] != ' ')
                                    HTTPRequest.Method += (char) buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    ParserState = HttpParserState.URL;
                                }
                                break;
                            case HttpParserState.URL:
                                if (data[ndx] == '?')
                                {
                                    ndx++;
                                    hKey = "";
                                    HTTPRequest.Execute = true;
                                    _urlParameters = new Dictionary<string, string>();
                                    ParserState = HttpParserState.URLPARM;
                                }
                                else if (data[ndx] != ' ')
                                    HTTPRequest.URL += (char) buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    HTTPRequest.URL = WebUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = HttpParserState.VERSION;
                                }
                                break;
                            case HttpParserState.URLPARM:
                                if (data[ndx] == '=')
                                {
                                    ndx++;
                                    hValue = "";
                                    ParserState = HttpParserState.URLVALUE;
                                }
                                else if (data[ndx] == ' ')
                                {
                                    ndx++;

                                    HTTPRequest.URL = WebUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = HttpParserState.VERSION;
                                }
                                else
                                {
                                    hKey += (char) buffer.GetByte(ndx++);
                                }
                                break;
                            case HttpParserState.URLVALUE:
                                if (data[ndx] == '&')
                                {
                                    ndx++;
                                    hKey = WebUtility.UrlDecode(hKey);
                                    hValue = WebUtility.UrlDecode(hValue);
                                    _urlParameters[hKey] = _urlParameters.ContainsKey(hKey) ? _urlParameters[hKey] + ", " + hValue : hValue;
                                    hKey = "";
                                    ParserState = HttpParserState.URLPARM;
                                }
                                else if (data[ndx] == ' ')
                                {
                                    ndx++;
                                    hKey = WebUtility.UrlDecode(hKey);
                                    hValue = WebUtility.UrlDecode(hValue);
                                    _urlParameters[hKey] = _urlParameters.ContainsKey(hKey) ? _urlParameters[hKey] + ", " + hValue : hValue;
                                    HTTPRequest.URL = WebUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = HttpParserState.VERSION;
                                }
                                else
                                {
                                    hValue += (char) buffer.GetByte(ndx++);
                                }
                                break;
                            case HttpParserState.VERSION:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] != '\n')
                                    HTTPRequest.Version += (char) buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    hKey = "";
                                    _httpHeaders = new Dictionary<string, string>();
                                    ParserState = HttpParserState.HEADERKEY;
                                }
                                break;
                            case HttpParserState.HEADERKEY:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] == '\n')
                                {
                                    ndx++;
                                    if (_httpHeaders.ContainsKey("Content-Length"))
                                    {
                                        HTTPRequest.BodySize = Convert.ToInt32(_httpHeaders["Content-Length"]);
                                        ParserState = HttpParserState.BODY;
                                    }
                                    else
                                        ParserState = HttpParserState.OK;
                                }
                                else if (data[ndx] == ':')
                                    ndx++;
                                else if (data[ndx] != ' ')
                                    hKey += (char) buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    hValue = "";
                                    ParserState = HttpParserState.HEADERVALUE;
                                }
                                break;
                            case HttpParserState.HEADERVALUE:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] != '\n')
                                    hValue += (char) buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    _httpHeaders.Add(hKey, hValue);
                                    hKey = "";
                                    ParserState = HttpParserState.HEADERKEY;
                                }
                                break;
                            case HttpParserState.BODY:
                                // Append to request BodyData
                                HTTPRequest.BodyContent = Encoding.UTF8.GetString(data, 0, HTTPRequest.BodySize);
                                bfndx += dataRead - ndx;
                                ndx = dataRead;
                                if (HTTPRequest.BodySize <= bfndx)
                                {
                                    ParserState = HttpParserState.OK;
                                }
                                break;
                            //default:
                            //   ndx++;
                            //   break;
                        }
                    } while (ndx < dataRead);
                }
                ;

                // Print out the received message to the console.
                Debug.WriteLine("You received the following message : \n" + requestString);
                if (_httpHeaders != null)
                    HTTPRequest.Headers = _httpHeaders.AsEnumerable();
                if (_urlParameters != null)
                    HTTPRequest.UrlParameters = _urlParameters.AsEnumerable();

                return HTTPRequest;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return null;
        }
    }
}