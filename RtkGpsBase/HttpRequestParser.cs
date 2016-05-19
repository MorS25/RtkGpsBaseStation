using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using RtkGpsBase.Enums;

namespace RtkGpsBase{
    public sealed class HttpRequestParser
    {
        private readonly uint _bufferSize = 8192;
        private readonly HttpRequest _httpRequest;
        private HttpParserState _parserState;

        public HttpRequestParser(uint readBuffer)
        {
            _httpRequest = new HttpRequest();
            _bufferSize = readBuffer;
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
            Dictionary<string, string> httpHeaders = null;
            Dictionary<string, string> urlParameters = null;

            var data = new byte[_bufferSize];
            var requestString = new StringBuilder();
            var dataRead = _bufferSize;

            var buffer = data.AsBuffer();

            var hValue = "";
            var hKey = "";

            try
            {
                // binary data buffer index
                uint bfndx = 0;

                // Incoming message may be larger than the buffer size.
                while (dataRead == _bufferSize)
                {
                    await stream.ReadAsync(buffer, _bufferSize, InputStreamOptions.Partial);
                    requestString.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;

                    // read buffer index
                    uint ndx = 0;
                    do
                    {
                        switch (_parserState)
                        {
                            case HttpParserState.METHOD:
                                if (data[ndx] != ' ')
                                    _httpRequest.Method += (char)buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    _parserState = HttpParserState.URL;
                                }
                                break;
                            case HttpParserState.URL:
                                if (data[ndx] == '?')
                                {
                                    ndx++;
                                    hKey = "";
                                    _httpRequest.Execute = true;
                                    urlParameters = new Dictionary<string, string>();
                                    _parserState = HttpParserState.URLPARM;
                                }
                                else if (data[ndx] != ' ')
                                    _httpRequest.URL += (char)buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    _httpRequest.URL = WebUtility.UrlDecode(_httpRequest.URL);
                                    _parserState = HttpParserState.VERSION;
                                }
                                break;
                            case HttpParserState.URLPARM:
                                if (data[ndx] == '=')
                                {
                                    ndx++;
                                    hValue = "";
                                    _parserState = HttpParserState.URLVALUE;
                                }
                                else if (data[ndx] == ' ')
                                {
                                    ndx++;

                                    _httpRequest.URL = WebUtility.UrlDecode(_httpRequest.URL);
                                    _parserState = HttpParserState.VERSION;
                                }
                                else
                                {
                                    hKey += (char)buffer.GetByte(ndx++);
                                }
                                break;
                            case HttpParserState.URLVALUE:
                                if (data[ndx] == '&')
                                {
                                    ndx++;
                                    hKey = WebUtility.UrlDecode(hKey);
                                    hValue = WebUtility.UrlDecode(hValue);
                                    urlParameters[hKey] = urlParameters.ContainsKey(hKey) ? urlParameters[hKey] + ", " + hValue : hValue;
                                    hKey = "";
                                    _parserState = HttpParserState.URLPARM;
                                }
                                else if (data[ndx] == ' ')
                                {
                                    ndx++;
                                    hKey = WebUtility.UrlDecode(hKey);
                                    hValue = WebUtility.UrlDecode(hValue);
                                    urlParameters[hKey] = urlParameters.ContainsKey(hKey) ? urlParameters[hKey] + ", " + hValue : hValue;
                                    _httpRequest.URL = WebUtility.UrlDecode(_httpRequest.URL);
                                    _parserState = HttpParserState.VERSION;
                                }
                                else
                                {
                                    hValue += (char)buffer.GetByte(ndx++);
                                }
                                break;
                            case HttpParserState.VERSION:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] != '\n')
                                    _httpRequest.Version += (char)buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    hKey = "";
                                    httpHeaders = new Dictionary<string, string>();
                                    _parserState = HttpParserState.HEADERKEY;
                                }
                                break;
                            case HttpParserState.HEADERKEY:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] == '\n')
                                {
                                    ndx++;
                                    if (httpHeaders.ContainsKey("Content-Length"))
                                    {
                                        _httpRequest.BodySize = Convert.ToInt32(httpHeaders["Content-Length"]);
                                        _parserState = HttpParserState.BODY;
                                    }
                                    else
                                        _parserState = HttpParserState.OK;
                                }
                                else if (data[ndx] == ':')
                                    ndx++;
                                else if (data[ndx] != ' ')
                                    hKey += (char)buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    hValue = "";
                                    _parserState = HttpParserState.HEADERVALUE;
                                }
                                break;
                            case HttpParserState.HEADERVALUE:
                                if (data[ndx] == '\r')
                                    ndx++;
                                else if (data[ndx] != '\n')
                                    hValue += (char)buffer.GetByte(ndx++);
                                else
                                {
                                    ndx++;
                                    httpHeaders.Add(hKey, hValue);
                                    hKey = "";
                                    _parserState = HttpParserState.HEADERKEY;
                                }
                                break;
                            case HttpParserState.BODY:
                                // Append to request BodyData
                                _httpRequest.BodyContent = Encoding.UTF8.GetString(data, 0, _httpRequest.BodySize);
                                bfndx += dataRead - ndx;
                                ndx = dataRead;
                                if (_httpRequest.BodySize <= bfndx)
                                {
                                    _parserState = HttpParserState.OK;
                                }
                                break;
                                //default:
                                //   ndx++;
                                //   break;
                        }
                    } while (ndx < dataRead);
                }
                
                if (httpHeaders != null)
                    _httpRequest.Headers = httpHeaders.AsEnumerable();
                if (urlParameters != null)
                    _httpRequest.UrlParameters = urlParameters.AsEnumerable();

                return _httpRequest;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return null;
        }
    }
}