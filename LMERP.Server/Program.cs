
using LMERP.Server.Auth;
using LMERP.Application;
using LMERP.Infrastructure;
using LMERP.Infrastructure.Identity;
using LMERP.Infrastructure.Persistence;
using LMERP.Server.Middleware;
using LMERP.Server.Responses;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LMERP.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var signingKey = builder.Configuration["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Configure Jwt:SigningKey with a secret of at least 32 characters.");
                signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                builder.Configuration["Jwt:SigningKey"] = signingKey;
            }
            if (Encoding.UTF8.GetByteCount(signingKey) < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddIdentityCore<LoanUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            }).AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<LoanDbContext>().AddSignInManager();
            builder.Services.AddScoped<JwtTokenIssuer>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(BaseResponse<object>.Failed("Authentication is required."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(BaseResponse<object>.Failed("You do not have permission to perform this action."));
                    }
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState.Values.SelectMany(value => value.Errors)
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The supplied value is invalid." : error.ErrorMessage).ToArray();
                    return new BadRequestObjectResult(BaseResponse<object>.Failed("Request validation failed.", errors));
                };
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
                await db.Database.MigrateAsync();
                if (app.Environment.IsDevelopment()) await LoanDbContext.SeedAsync(db);
                await IdentitySeeder.SeedAsync(scope.ServiceProvider, builder.Configuration);
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseMiddleware<ApiExceptionMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Map("/api/{**path}", (HttpContext context) =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return context.Response.WriteAsJsonAsync(BaseResponse<object>.Failed("API endpoint was not found."));
            });

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
