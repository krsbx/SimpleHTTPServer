using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;

namespace SimpleHTTPServer {
  class Program {
    static void Main (string[] args) {
      string folder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

      HttpServer myServer = new HttpServer(folder, 8084);

      string url = $"http:\\\\127.0.0.1:{myServer.Port}";

      Console.WriteLine($"Server is running on {url}");
      OpenUrl(url);
      Console.ReadLine();
    }

    static void OpenUrl (string url) {
      try {
        Process.Start(url);
      } catch {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          url = url.Replace("&", "^&");
          Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
          Process.Start("xdg-open", url);
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          Process.Start("open", url);
        } else {
          throw;
        }
      }
    }
  }
}
