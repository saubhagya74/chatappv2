
using app.Data;
using app.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

namespace app
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Configure database
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DatabaseConnection")));
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // 2. Controllers & SignalR
            builder.Services.AddSingleton<SnowFlakeGen>();
            builder.Services.AddSignalR();
            builder.Services
            .AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            // Configure static files for profile pictures
            builder.Services.Configure<StaticFileOptions>(options =>
            {
                options.ServeUnknownFileTypes = true;
                var provider = new FileExtensionContentTypeProvider();
                provider.Mappings[".jpeg"] = "image/jpeg";
                provider.Mappings[".jpg"] = "image/jpeg";
                provider.Mappings[".png"] = "image/png";
                options.ContentTypeProvider = provider;
            });


            // 3. Swagger
            builder.Services.AddOpenApi();

            // 4. Authentication + JWT configuration with SignalR support
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["AppSettings:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["AppSettings:Audience"],
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["AppSettings:Token"]!)),
                        ClockSkew = TimeSpan.Zero // optional: makes token expiry strict
                    };

                    // Required for SignalR to read token from query string
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
     (path.StartsWithSegments("/ChatHub") || path.StartsWithSegments("/VideoChatHub")))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            // 5. CORS setup
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDevClient", policy =>
                {
                    policy.WithOrigins(
                        "http://localhost:4200",
                "http://192.168.206.1:4200",
                "http://192.168.1.66:4200",
                "http://192.168.100.1:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // needed for cookies or SignalR
                });
            });

            var app = builder.Build();

            // 6. Middleware pipeline
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            // Serve static files (including profile pictures)
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(builder.Environment.WebRootPath)),
                RequestPath = ""
            });

            app.UseRouting(); //  Required before CORS/Auth

            app.UseCors("AllowAngularDevClient"); //  Must come before Auth

            app.UseAuthentication();
            app.UseAuthorization();
         
            app.MapControllers();
            app.MapHub<ChatHub>("/ChatHub"); 
            app.MapHub<VideoChatHub>("/VideoChatHub"); //  Match Angular path exactly

            app.Run();
        }
    }
}
