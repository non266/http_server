using System;
using System.IO;
using System.Net;

namespace httpserver
{
    class HttpServer
    {
        private HttpListener _listener;
        private string _rootPath;
        private int _port = 80;

        public HttpServer(string rootPath)
        {
            _rootPath = rootPath;
        }
        public void port(int port)
        {
            _port = port;
        }
        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
            _listener.Start();
            Console.WriteLine("Server started on port " + _port);

            while (true)
            {
                var context = _listener.GetContext();
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "GET")
                {
                    ServeFile(request, response);
                }
                else if (request.HttpMethod == "PUT")
                {
                    SaveFile(request, response);
                }
                else
                {
                    response.StatusCode = 405;
                    response.Close();
                }
            }
        }

        private void ServeFile(HttpListenerRequest request, HttpListenerResponse response)
        {
            var filePath = Path.Combine(_rootPath, request.Url.LocalPath.Substring(1));
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                var rangeHeader = request.Headers["Range"];

                if (!string.IsNullOrEmpty(rangeHeader))
                {
                    int f1 = rangeHeader.IndexOf("=");
                    int f2 = rangeHeader.IndexOf("-");
                    string r = rangeHeader.Substring(f1, f2 - f1);
                    var range = GetRange(r, fileInfo.Length);
                    response.StatusCode = 206;
                    response.Headers.Add("Content-Range", $"bytes {range.Start}-{range.End}/{fileInfo.Length}");
                    response.ContentLength64 = range.Length;
                    Console.WriteLine(DateTime.Now + "     Get " + request.Url.LocalPath + "    Range" + r);
                    ServeRange(filePath, range, response.OutputStream);
                }
                else
                {
                    Console.WriteLine(DateTime.Now + "     Get " + request.Url.LocalPath);
                    response.ContentType = GetContentType(filePath);
                    response.ContentLength64 = fileInfo.Length;
                    ServeFile(filePath, response.OutputStream);
                }
            }
            else
            {
                response.StatusCode = 404;
            }

            response.Close();
        }

        private void ServeFile(string filePath, Stream outputStream)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                fileStream.CopyTo(outputStream);
            }
        }

        private void ServeRange(string filePath, Range range, Stream outputStream)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                fileStream.Seek(range.Start, SeekOrigin.Begin);

                var buffer = new byte[4096];
                var remaining = range.Length;
                while (remaining > 0)
                {
                    var bytesRead = fileStream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    try
                    {
                        outputStream.Write(buffer, 0, bytesRead);
                        remaining -= bytesRead;
                    }
                    catch
                    {

                    }
                }
            }
        }

        private void SaveFile(HttpListenerRequest request, HttpListenerResponse response)
        {
            var filePath = Path.Combine(_rootPath, request.Url.LocalPath.Substring(1));
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            using (var fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.Write))
            {
                var rangeHeader = request.Headers["Content-Range"];
                if (!string.IsNullOrEmpty(rangeHeader))
                {
                    var range = GetRange(rangeHeader, fileInfo.Length);
                    if (range.Start != fileStream.Length)
                    {
                        response.StatusCode = 400;
                        response.Close();
                        return;
                    }
                    fileStream.Seek(range.Start, SeekOrigin.Begin);
                }
                else
                {
                    fileStream.SetLength(0);
                }

                request.InputStream.CopyTo(fileStream);
            }

            response.Close();
        }

        private static string GetContentType(string filePath)
        {
            switch (Path.GetExtension(filePath))
            {
                case ".txt":
                    return "text/plain";
                case ".html":
                    return "text/html";
                case ".js":
                    return "application/javascript";
                case ".css":
                    return "text/css";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".mp4":
                    return "video/mp4";
                case "":
                    return "text/html";
                case ".mp3":
                    return "audio/mp3";
                default:
                    return "application/octet-stream";
            }
        }

        private static Range GetRange(string rangeHeader, long fileSize)
        {
            var parts = rangeHeader.Split('=');
            var range = parts[1].Split('-');
            var start = long.Parse(range[0]);
            var end = range.Length > 1 ? long.Parse(range[1]) : fileSize - 1;
            return new Range(start, end);
        }

        private struct Range
        {
            public long Start;
            public long End;

            public Range(long start, long end)
            {
                Start = start;
                End = end;
            }

            public long Length => End - Start + 1;
        }
    }
}
