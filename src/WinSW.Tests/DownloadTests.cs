using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WinSW.Tests.Util;
using Xunit;

namespace WinSW.Tests
{
    public class DownloadTests : IDisposable
    {
        private readonly HttpListener globalListener = new();

        private readonly byte[] contents = { 0x57, 0x69, 0x6e, 0x53, 0x57 };

        private readonly string globalPrefix;

        public DownloadTests()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            string prefix = $"http://localhost:{port}/";
            this.globalListener.Prefixes.Add(prefix);
            this.globalPrefix = prefix;
            {
                tcpListener.Stop();
                this.globalListener.Start();
            }
        }

        public void Dispose()
        {
            this.globalListener.Stop();
            this.globalListener.Close();
        }

        private async Task TestClientServerAsync(Func<string, string, Task> client, Action<HttpListenerContext> server, AuthenticationSchemes authenticationSchemes = AuthenticationSchemes.Anonymous, [CallerMemberName] string path = null)
        {
            var listener = new HttpListener();
            string prefix = $"{this.globalPrefix}{path}/";
            listener.Prefixes.Add(prefix);
            listener.AuthenticationSchemes = authenticationSchemes;
            listener.Start();

            Task serverTask = null;
            try
            {
                serverTask = ListenAsync();

                string dest = Path.GetTempFileName();
                try
                {
                    await client(prefix, dest);
                }
                finally
                {
                    File.Delete(dest);
                }
            }
            finally
            {
                listener.Stop();
                listener.Close();
                if (serverTask != null)
                {
                    await serverTask;
                }
            }

            async Task ListenAsync()
            {
                var context = await listener.GetContextAsync();
                try
                {
                    server(context);
                }
                catch
                {
                    context.Response.Abort();
                }
            }
        }

        [Fact]
        public async Task TestHttpAsync()
        {
            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    await new Download(source, dest).PerformAsync();
                    Assert.Equal(this.contents, File.ReadAllBytes(dest));
                },
                context =>
                {
                    context.Response.OutputStream.Write(this.contents, 0, this.contents.Length);
                    context.Response.Close();
                });
        }

        [Fact]
        public async Task TestHttp_NoAuthAsync()
        {
            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    await new Download(source, dest, false, Download.AuthType.None).PerformAsync();
                    Assert.Equal(this.contents, File.ReadAllBytes(dest));
                },
                context =>
                {
                    if (((WebHeaderCollection)context.Request.Headers)[HttpRequestHeader.Authorization] != null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                    }

                    context.Response.OutputStream.Write(this.contents, 0, this.contents.Length);
                    context.Response.Close();
                });
        }

        [Fact]
        public async Task TestHttp_BasicAuthAsync()
        {
            const string username = nameof(username);
            const string password = nameof(password);

            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    await new Download(source, dest, false, Download.AuthType.Basic, username, password, true).PerformAsync();
                    Assert.Equal(this.contents, File.ReadAllBytes(dest));
                },
                context =>
                {
                    var identity = (HttpListenerBasicIdentity)context.User.Identity;
                    if (identity.Name != username || identity.Password != password)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        context.Response.Close();
                    }

                    context.Response.OutputStream.Write(this.contents, 0, this.contents.Length);
                    context.Response.Close();
                },
                AuthenticationSchemes.Basic);
        }

        [Fact]
        public async Task TestHttp_IfModifiedSince_ModifiedAsync()
        {
            var lastModified = DateTime.Now.TrimToSeconds();
            var prevModified = lastModified.AddDays(-1);

            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    File.WriteAllBytes(dest, this.contents);
                    File.SetLastWriteTime(dest, prevModified);
                    await new Download(source, dest).PerformAsync();
                    Assert.Equal(lastModified, File.GetLastWriteTime(dest));
                    Assert.NotEqual(this.contents, File.ReadAllBytes(dest));
                },
                context =>
                {
                    string ifModifiedSince = ((WebHeaderCollection)context.Request.Headers)[HttpRequestHeader.IfModifiedSince];
                    if (ifModifiedSince != null && DateTime.Parse(ifModifiedSince) >= lastModified)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    }

                    context.Response.Headers[HttpResponseHeader.LastModified] = lastModified.ToUniversalTime().ToString("r");
                    context.Response.Close();
                });
        }

        [Fact]
        public async Task TestHttp_IfModifiedSince_NotModifiedAsync()
        {
            var lastModified = DateTime.Now.TrimToSeconds();

            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    File.WriteAllBytes(dest, this.contents);
                    File.SetLastWriteTime(dest, lastModified);
                    await new Download(source, dest).PerformAsync();
                    Assert.Equal(lastModified, File.GetLastWriteTime(dest));
                    Assert.Equal(this.contents, File.ReadAllBytes(dest));
                },
                context =>
                {
                    string ifModifiedSince = ((WebHeaderCollection)context.Request.Headers)[HttpRequestHeader.IfModifiedSince];
                    if (ifModifiedSince != null && DateTime.Parse(ifModifiedSince) >= lastModified)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    }

                    context.Response.Headers[HttpResponseHeader.LastModified] = lastModified.ToUniversalTime().ToString("r");
                    context.Response.Close();
                });
        }

        [Fact]
        public async Task TestHttp_NotFound_ThrowsAsync()
        {
            await this.TestClientServerAsync(
                async (source, dest) =>
                {
                    var exception = await Assert.ThrowsAsync<IOException>(
                        async () => await new Download(source, dest, true).PerformAsync());

                    var inner = Assert.IsType<WebException>(exception.InnerException);
                    Assert.Equal(WebExceptionStatus.ProtocolError, inner.Status);
                },
                context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                });
        }
    }
}
