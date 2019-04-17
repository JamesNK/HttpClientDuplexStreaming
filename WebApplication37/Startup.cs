using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApplication37
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("Logger");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/response-body-flushasync", async context =>
                {
                    await ProcessRequestCore(context, logger, c => c.Response.Body.FlushAsync());
                });

                endpoints.MapPost("/response-startasync", async context =>
                {
                    await ProcessRequestCore(context, logger, c => c.Response.StartAsync());
                });

                endpoints.MapPost("/response-bodywriter-flushasync", async context =>
                {
                    await ProcessRequestCore(context, logger, c => c.Response.BodyWriter.FlushAsync().AsTask());
                });
            });
        }

        private static async Task ProcessRequestCore(HttpContext context, ILogger logger, Func<HttpContext, Task> sendHeaders)
        {
            context.Response.Headers["test-header"] = "value!";

            logger.LogWarning("Sending headers");
            await sendHeaders(context);

            await Task.Delay(TimeSpan.FromSeconds(5));

            logger.LogWarning("Reading request content and echoing it back");
            var buffer = new byte[2048];
            int readContent;
            while ((readContent = await context.Request.Body.ReadAsync(buffer)) > 0)
            {
                Console.WriteLine($"Content received - {readContent} bytes");

                await context.Response.Body.WriteAsync(buffer, 0, readContent);
                await context.Response.Body.FlushAsync();
            }

            logger.LogWarning("Finished reading request");
        }
    }
}
