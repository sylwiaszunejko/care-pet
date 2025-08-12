using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Cassandra; // DataStax Cassandra C# driver

namespace CarePet.Server
{
    public class App
    {
        public static void Main(string[] args)
        {
            var config = Config.Parse(new Config(), args);

            var builder = WebApplication.CreateBuilder(args);

            // Register Config as singleton
            builder.Services.AddSingleton(config);

            // Register Cassandra session as singleton
            builder.Services.AddSingleton<ISession>(sp =>
            {
                var cfg = sp.GetRequiredService<Config>();
                return cfg.Builder(Config.Keyspace).Build().Connect(Config.Keyspace);
            });

            // Add controllers (API endpoints)
            builder.Services.AddControllers();

            // Add Swagger / OpenAPI
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "CarePet",
                    Version = "0.1",
                    Description = "CarePet: An Example IoT Use Case for Hands-On App Developers",
                    License = new OpenApiLicense
                    {
                        Name = "Apache 2.0",
                        Url = new Uri("https://github.com/scylladb/care-pet/blob/master/LICENSE")
                    },
                    Contact = new OpenApiContact
                    {
                        Url = new Uri("https://github.com/scylladb/care-pet")
                    }
                });
                c.IncludeXmlComments("CarePet.Server.xml", includeControllerXmlComments: true);
                c.ExternalDocumentation = new OpenApiExternalDocumentation
                {
                    Url = new Uri("https://scylladb.github.io/care-pet/master/index.html"),
                    Description = "CarePet: An Example IoT Use Case for Hands-On App Developers"
                };
            });

            var app = builder.Build();

            // Enable Swagger UI
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CarePet v0.1");
                    c.RoutePrefix = string.Empty; // Swagger at root
                });
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
