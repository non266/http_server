using httpserver;

public class SimpleHttpServer
{
    public static void Main(string[] args)
    {
        HttpServer server = new HttpServer(System.Environment.CurrentDirectory);
        server.port(8080);
        server.Start();

    }
}