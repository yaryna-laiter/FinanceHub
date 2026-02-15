using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FinanceGub.Application;
using FinanceGub.Application.Identity;
using FinanceGub.Application.Interfaces;
using FinanceGub.Application.Interfaces.Serviсes;
using FinanceHub.Core.Entities;
using FinanceHub.Infrastructure;
using FinanceHub.Infrastructure.Data;
using FinanceHub.Infrastructure.Services;
using FinanceHub.Infrastructure.SignalR;
using FinanceHub.Middleware;
using FinanceHub.SignalR;
using FinanceHub.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

string connectionString,
    jwtIssuerSecret,
    jwtAudienceSecret,
    jwtKeySecret,
    blobStorageConnectionString,
    containerName,
    googleClientId,
    googleClientSecret;

if (builder.Environment.IsProduction())
{
    try
    {
        var keyVaultUrl = new Uri($"https://{builder.Configuration["FinHubKeyVault"]}.vault.azure.net/");
        var client = new SecretClient(keyVaultUrl, new DefaultAzureCredential());

        connectionString = (await client.GetSecretAsync("DbConnectionString")).Value.Value;
        jwtIssuerSecret = (await client.GetSecretAsync("JwtIssuer")).Value.Value;
        jwtAudienceSecret = (await client.GetSecretAsync("JwtAudience")).Value.Value;
        jwtKeySecret = (await client.GetSecretAsync("JwtKey")).Value.Value;
        googleClientId = (await client.GetSecretAsync("GoogleClientId")).Value.Value;
        googleClientSecret = (await client.GetSecretAsync("GoogleClientSecret")).Value.Value;
        blobStorageConnectionString = (await client.GetSecretAsync("AzureBlobStorageConnectionString")).Value.Value;
        containerName = (await client.GetSecretAsync("ProfilePicturesContainer")).Value.Value;

        builder.Services.AddTransient<IAzureBlobStorageService>(provider =>
            new AzureBlobStorageService(blobStorageConnectionString, containerName));

        builder.Services.AddDbContext<FinHubDbContext>(options =>
            options.UseNpgsql(connectionString));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error retrieving secrets from Key Vault: {ex.Message}");
        throw;
    }
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    jwtIssuerSecret = builder.Configuration["JwtIssuer"];
    jwtAudienceSecret = builder.Configuration["JwtAudience"];
    jwtKeySecret = builder.Configuration["JwtKey"];
    googleClientId = builder.Configuration["GoogleClientId"];
    googleClientSecret = builder.Configuration["GoogleClientSecret"];

    builder.Services.AddTransient<IAzureBlobStorageService, LocalAzureBlobStorageService>();

    builder.Services.AddDbContext<FinHubDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Register Identity with custom User and Role types
builder.Services.AddIdentity<User, AppRole>(options =>
    {
        // Configure Identity options if needed
    })
    .AddRoles<AppRole>() // Specify the role type (AppRole)
    .AddEntityFrameworkStores<FinHubDbContext>()
    .AddRoleManager<RoleManager<AppRole>>() // Register the custom RoleManager
    .AddUserManager<UserManager<User>>() // Optional, to explicitly register the custom UserManager
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuerSecret,
            ValidAudience = jwtAudienceSecret,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKeySecret))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// Налаштування аутентифікації через Google
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/user";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(IdentityData.AdminUserPolicyName, p =>
        p.RequireClaim(IdentityData.AdminUserClaimName, "true"));
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSignalR();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddScoped<ILikeHub, LikeHub>();


// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder
            .WithOrigins("http://localhost:4200") // or .AllowAnyOrigin() for dev
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Pagination");
    });
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FinHubDbContext>();

    context.Database.EnsureCreated();

    var connection = context.Database.GetDbConnection();
    if (connection is Microsoft.Data.Sqlite.SqliteConnection)
    {
        var tableExists = context.Database.ExecuteSqlRaw(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='Connections';") > 0;
        if (tableExists)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Connections\"");
        }
    }
    else
    {
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Connections\"");
    }
}


app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1"); });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PresenceHub>("hubs/presence");
app.MapHub<MessageHub>("hubs/message");

app.Run();