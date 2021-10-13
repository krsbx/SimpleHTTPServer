// MIT License - Copyright (c) 2016 Can Güney Aksakalli
// https://aksakalli.github.io/2014/02/24/simple-http-server-with-csparp.html

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Web;
using System.Linq;

class HttpServer {
  private readonly string[] _indexFiles = {
        "index.html",
        "index.htm",
        "default.html",
        "default.htm"
    };

  private static IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
        #region extension to MIME type list
        {".asf", "video/x-ms-asf"},
        {".asx", "video/x-ms-asf"},
        {".avi", "video/x-msvideo"},
        {".bin", "application/octet-stream"},
        {".cco", "application/x-cocoa"},
        {".crt", "application/x-x509-ca-cert"},
        {".css", "text/css"},
        {".deb", "application/octet-stream"},
        {".der", "application/x-x509-ca-cert"},
        {".dll", "application/octet-stream"},
        {".dmg", "application/octet-stream"},
        {".ear", "application/java-archive"},
        {".eot", "application/octet-stream"},
        {".exe", "application/octet-stream"},
        {".flv", "video/x-flv"},
        {".gif", "image/gif"},
        {".hqx", "application/mac-binhex40"},
        {".htc", "text/x-component"},
        {".htm", "text/html"},
        {".html", "text/html"},
        {".ico", "image/x-icon"},
        {".img", "application/octet-stream"},
        {".iso", "application/octet-stream"},
        {".jar", "application/java-archive"},
        {".jardiff", "application/x-java-archive-diff"},
        {".jng", "image/x-jng"},
        {".jnlp", "application/x-java-jnlp-file"},
        {".jpeg", "image/jpeg"},
        {".jpg", "image/jpeg"},
        {".js", "application/x-javascript"},
        {".mml", "text/mathml"},
        {".mng", "video/x-mng"},
        {".mov", "video/quicktime"},
        {".mp3", "audio/mpeg"},
        {".mpeg", "video/mpeg"},
        {".mpg", "video/mpeg"},
        {".msi", "application/octet-stream"},
        {".msm", "application/octet-stream"},
        {".msp", "application/octet-stream"},
        {".pdb", "application/x-pilot"},
        {".pdf", "application/pdf"},
        {".pem", "application/x-x509-ca-cert"},
        {".pl", "application/x-perl"},
        {".pm", "application/x-perl"},
        {".png", "image/png"},
        {".prc", "application/x-pilot"},
        {".ra", "audio/x-realaudio"},
        {".rar", "application/x-rar-compressed"},
        {".rpm", "application/x-redhat-package-manager"},
        {".rss", "text/xml"},
        {".run", "application/x-makeself"},
        {".sea", "application/x-sea"},
        {".shtml", "text/html"},
        {".sit", "application/x-stuffit"},
        {".swf", "application/x-shockwave-flash"},
        {".tcl", "application/x-tcl"},
        {".tk", "application/x-tcl"},
        {".txt", "text/plain"},
        {".war", "application/java-archive"},
        {".wbmp", "image/vnd.wap.wbmp"},
        {".wmv", "video/x-ms-wmv"},
        {".xml", "text/xml"},
        {".xpi", "application/x-xpinstall"},
        {".zip", "application/zip"},
        {".wasm", "application/wasm"},
        #endregion
    };
  private static IDictionary<string, string> _contentEncodingMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
    {".gz", "gzip"},
  };
  private Thread _serverThread;
  private string _rootDirectory;
  private HttpListener _listener;
  private int _port;

  public int Port {
    get { return _port; }
    private set { }
  }

  /// <summary>
  /// Construct server with given port.
  /// </summary>
  /// <param name="path">Directory path to serve.</param>
  /// <param name="port">Port of the server.</param>
  public HttpServer (string path, int port = 8000) {
    port = GetOpenPort(port);
    this.Initialize(path, port);
  }

  public static int GetOpenPort(int startPort = 2555) {
    var usedPorts = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(p => p.Port).ToList();
    return Enumerable.Range(startPort, 999).Where(port => !usedPorts.Contains(port)).FirstOrDefault();
  }

  /// <summary>
  /// Stop server and dispose all functions.
  /// </summary>
  public void Stop () {
    _serverThread.Abort();
    _listener.Stop();
  }

  private void Listen () {
    _listener = new HttpListener();
    _listener.Prefixes.Add("http://127.0.0.1:" + _port.ToString() + "/");
    _listener.Start();
    while (true) {
      try {
        HttpListenerContext context = _listener.GetContext();
        Process(context);
      } catch (Exception ex) {
        System.Console.WriteLine(ex);
      }
    }
  }

  private bool IsPathAllowed(string path) {
    try {
      return new Uri(_rootDirectory).IsBaseOf(new Uri(path));
    }
    catch (Exception) {
      return false;
    }
  }

  private void Process (HttpListenerContext context) {
    string filename = context.Request.Url.AbsolutePath;
    filename = HttpUtility.UrlDecode(filename);
    Console.WriteLine(filename);
    filename = filename.Substring(1);

    if (string.IsNullOrEmpty(filename)) {
      foreach (string indexFile in _indexFiles) {
        if (File.Exists(Path.Combine(_rootDirectory, indexFile))) {
          filename = indexFile;
          break;
        }
      }
    }

    filename = Path.Combine(_rootDirectory, filename);

    if (!IsPathAllowed(filename)) {
      context.Response.StatusCode = (int) HttpStatusCode.Forbidden;
    }
    else if (File.Exists(filename)) {
      try {
        Stream input = new FileStream(filename, FileMode.Open);

        //Adding permanent http response headers
        string mime;
        context.Response.ContentType = _mimeTypeMappings.TryGetValue(Path.GetExtension(filename), out mime) ? mime : "application/octet-stream";
        context.Response.ContentLength64 = input.Length;
        context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
        context.Response.AddHeader("Last-Modified", System.IO.File.GetLastWriteTime(filename).ToString("r"));

        if (_contentEncodingMappings.TryGetValue(Path.GetExtension(filename), out var encoding)) {
          context.Response.AddHeader("Content-Encoding", encoding);
        }
        
        byte[] buffer = new byte[ 1024 * 16 ];
        int nbytes;
        while (( nbytes = input.Read(buffer, 0, buffer.Length) ) > 0)
          context.Response.OutputStream.Write(buffer, 0, nbytes);
        input.Close();

        context.Response.StatusCode = (int) HttpStatusCode.OK;
        context.Response.OutputStream.Flush();
      } catch (Exception ex) {
        System.Console.WriteLine(ex);

        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
      }

    } else if (Directory.Exists(filename)) {
      // File browser

      filename = Path.GetFullPath(filename + "/");
      using (var writer = new StreamWriter(context.Response.OutputStream)) {
        void WriteLink(string link, string text, string icon) => writer.Write("<a href=\"{0}\"><img src=\"{2}\"> {1}</a><br>", link, text, icon);
        void WriteUri(Uri uri, string icon) => WriteLink(uri.ToString(), HttpUtility.HtmlEncode(HttpUtility.UrlDecode(uri.ToString())), icon);

        var baseUri = new Uri(filename);
        if (filename != _rootDirectory) WriteLink("../", "Back", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAUElEQVR42mNgoAP4jwMTp3l+AXaMbsh/fJiQIf8fzMWPcRlCtAEwQ3CFBUFNBOSJs5EkA3A5lygDCMUIXgMIaUY3hHZhMDgMICFxER942DAAVeyEg1KZUZsAAAAASUVORK5CYII=");
        writer.Write("<p>");
        foreach (var elem in Directory.GetDirectories(filename)) {
          WriteUri(baseUri.MakeRelativeUri(new Uri(elem + "/")), "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAOklEQVR42mNgoAP4jwMTp3l+AXaMbsh/fJiQIf8fzMWPcRlCtAEwQ3CFBVEG4DF01IBRA/4TzAeEMAD1u8MF0hnk0QAAAABJRU5ErkJggg==");
        }
        writer.Write("<p>");
        foreach (var elem in Directory.GetFiles(filename)) {
          WriteUri(baseUri.MakeRelativeUri(new Uri(elem)), "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQAgMAAABinRfyAAAACVBMVEUAAAAAAAD///+D3c/SAAAAAXRSTlMAQObYZgAAAEBJREFUCNdjYAAB0dAABgapVVMYGCRnRoJYsxwYJCOjJgBZU0MYJMPCUoCsVUsYJKdOhbFSU2GssFAoSzQ0NAQADkYWX7FoSfoAAAAASUVORK5CYII=");
        }
      }
    } else {
      context.Response.StatusCode = (int) HttpStatusCode.NotFound;
    }

    context.Response.OutputStream.Close();
  }

  private void Initialize (string path, int port) {
    this._rootDirectory = path;
    this._port = port;
    _serverThread = new Thread(this.Listen);
    _serverThread.Start();
  }
}
