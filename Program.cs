using Freelancing.Data;
using Freelancing.Hubs;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/startup-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .WriteTo.Console()
            .WriteTo.File("Logs/app-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true));

    // Load environment variables
    if (File.Exists(".env") && builder.Environment.IsDevelopment())
    {
        DotNetEnv.Env.Load();
    }

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Email:SmtpServer"] = Environment.GetEnvironmentVariable("SMTP_SERVER"),
        ["Email:SmtpPort"] = "587",
        ["Email:SmtpUsername"] = Environment.GetEnvironmentVariable("SMTP_USERNAME"),
        ["Email:SmtpPassword"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
        ["Email:FromEmail"] = Environment.GetEnvironmentVariable("FROM_EMAIL"),
        ["Email:FromName"] = "GigHub",
        ["Email:EnableSsl"] = "true"
    });

    // Configure services
    ConfigureServices(builder);

    var app = builder.Build();

    // Configure pipeline
    await ConfigurePipelineAsync(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;
    var configuration = builder.Configuration;
    var environment = builder.Environment;

    if (File.Exists(".env"))
    {
        DotNetEnv.Env.Load();
    }

    // Add environment variable mapping for Email configuration (NEW)
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Email:SmtpServer"] = Environment.GetEnvironmentVariable("SMTP_SERVER"),
        ["Email:SmtpPort"] = "587",
        ["Email:SmtpUsername"] = Environment.GetEnvironmentVariable("SMTP_USERNAME"),
        ["Email:SmtpPassword"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
        ["Email:FromEmail"] = Environment.GetEnvironmentVariable("FROM_EMAIL"),
        ["Email:FromName"] = "GigHub",
        ["Email:EnableSsl"] = "true"
    });

    // Add controllers and views
    services.AddControllersWithViews();
    /*services.AddControllersWithViews(options =>
    {
        *//*// Only require HTTPS in production when HTTPS is not disabled
        var disableHttpsRequirement = Environment.GetEnvironmentVariable("DISABLE_HTTPS_REQUIREMENT") == "true";

        if (!environment.IsDevelopment() && !disableHttpsRequirement)
        {
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequireHttpsAttribute());
        }*//*
    });*/

    services.AddRazorPages();

    services.AddRateLimiter(options =>
    {
        // Global rate limiter (general protection)
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            context =>
            {
                // Use IP address for partitioning
                var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                              ?? context.Connection.RemoteIpAddress?.ToString()
                              ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 200, // 200 requests per minute per IP
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

        // Authentication endpoints (stricter limits)
        options.AddPolicy("AuthPolicy", context =>
        {
            var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: clientIp,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 10, // Only 10 login attempts per minute
                    Window = TimeSpan.FromMinutes(1)
                });
        });

        // API endpoints (moderate limits)
        options.AddPolicy("ApiPolicy", context =>
        {
            var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown";

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: clientIp,
                factory: partition => new SlidingWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6 // 6 segments of 10 seconds each
                });
        });

        // File upload endpoints (very strict)
        options.AddPolicy("UploadPolicy", context =>
        {
            var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: clientIp,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5, // Only 5 uploads per minute
                    Window = TimeSpan.FromMinutes(1)
                });
        });

        // Custom rejection response
        options.OnRejected = async (context, token) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var clientIp = context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                           ?? context.HttpContext.Connection.RemoteIpAddress?.ToString();

            logger.LogWarning("Rate limit exceeded for IP: {ClientIp}, Path: {Path}",
                clientIp, context.HttpContext.Request.Path);

            // For HTML requests, set session data and redirect back
            if (context.HttpContext.Request.Headers.Accept.ToString().Contains("text/html"))
            {
                context.HttpContext.Session.SetString("RateLimitExceeded", "true");
                context.HttpContext.Session.SetString("RateLimitMessage", "Too many requests. Please wait before trying again.");
                context.HttpContext.Session.SetInt32("RateLimitRetryAfter", 60);

                // Redirect back to the same URL
                var referer = context.HttpContext.Request.Headers.Referer.FirstOrDefault();
                if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                {
                    context.HttpContext.Response.Redirect(refererUri.PathAndQuery);
                }
                else
                {
                    context.HttpContext.Response.Redirect("/Account/Login");
                }
                return;
            }

            // For API calls, return JSON
            context.HttpContext.Response.StatusCode = 429;
            context.HttpContext.Response.ContentType = "application/json";
            var response = new
            {
                error = "Rate limit exceeded",
                message = "Too many requests. Please try again later.",
                retryAfter = 60
            };

            await context.HttpContext.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(response), token);
        };
    });

    // Configure forwarded headers for reverse proxy
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Add health checks
    services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Session configuration
    services.AddDistributedMemoryCache();
    services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        if (!environment.IsDevelopment())
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        }
    });

    // Database configuration
    var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                         ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
                         ?? configuration.GetConnectionString("Freelancing");

    // Clean the connection string
    var connectionString = rawConnectionString?.Trim();

    Console.WriteLine($"Raw connection string: '{rawConnectionString}'");
    Console.WriteLine($"Cleaned connection string: '{connectionString}'");

    // Test if we can parse it
    try
    {
        var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        Console.WriteLine($"Parsed successfully: Host={connBuilder.Host}, Database={connBuilder.Database}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Parse error: {ex.Message}");
        throw;
    }

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string not configured. Check environment variables.");
    }

    // Identity configuration
    ConfigureIdentity(services, environment);

    // Register application services
    RegisterApplicationServices(services);

    // SignalR configuration
    ConfigureSignalR(services, configuration, environment);

    // Security headers
    if (!environment.IsDevelopment())
    {
        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }

    // Add antiforgery
    services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.SuppressXFrameOptionsHeader = false;
    });
}

static void ConfigureIdentity(IServiceCollection services, IWebHostEnvironment environment)
{
    services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    });

    services.AddIdentity<UserAccount, IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

        // Sign in settings
        options.SignIn.RequireConfirmedEmail = !environment.IsDevelopment();
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/LogOut";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;

        if (!environment.IsDevelopment())
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        }
    });

    services.AddScoped<IUserClaimsPrincipalFactory<UserAccount>, CustomClaimsFactory>();
    services.AddScoped<IUserClaimsPrincipalFactory<UserAccount>, CustomUserClaimsPrincipalFactory>();
}

static void RegisterApplicationServices(IServiceCollection services)
{
    // Email service
    services.AddScoped<IEmailService, EmailService>();

    // Role seeder service
    services.AddScoped<IRoleSeederService, RoleSeederService>();
    services.AddScoped<AdminSeederService>();

    // Mentorship services
    services.AddScoped<IMentorshipMatchingService, MentorshipMatchingService>();
    services.AddScoped<IMentorshipSchedulingService, MentorshipSchedulingService>();

    // Security services
    services.AddScoped<IMessageEncryptionService, MessageEncryptionService>();
    services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
    services.AddScoped<IIdentityEncryptionService, IdentityEncryptionService>();

    // Business services
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IContractService, ContractService>();
    services.AddScoped<IContractTerminationService, ContractTerminationService>();
    services.AddScoped<IPdfGenerationService, PdfGenerationService>();
    services.AddScoped<IPdfService, PdfService>();
    services.AddScoped<IReportService, ReportService>();

    // Smart Hiring Services
    services.AddScoped<ISmartHiringFeatureService, SmartHiringFeatureService>();
    services.AddScoped<ISmartHiringService, SmartHiringService>();
    services.AddSingleton<ILocalRandomForestService, LocalRandomForestService>();
    services.AddHttpClient<LocalRandomForestService>();

    // Background services
    services.AddHostedService<UserCleanupHostedService>();
}

static void ConfigureSignalR(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
{
    var signalRBuilder = services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = environment.IsDevelopment();

        var maxMessageSize = configuration.GetValue<int?>("SignalR:MaximumReceiveMessageSize") ?? 10 * 1024 * 1024;
        options.MaximumReceiveMessageSize = maxMessageSize;

        var clientTimeout = configuration.GetValue<TimeSpan?>("SignalR:ClientTimeoutInterval") ?? TimeSpan.FromSeconds(60);
        options.ClientTimeoutInterval = clientTimeout;

        var handshakeTimeout = configuration.GetValue<TimeSpan?>("SignalR:HandshakeTimeout") ?? TimeSpan.FromSeconds(15);
        options.HandshakeTimeout = handshakeTimeout;
    });
}

static async Task ConfigurePipelineAsync(WebApplication app)
{
    var environment = app.Environment;

    // Configure forwarded headers first
    app.UseForwardedHeaders();

    // Configure error handling
    if (!environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // Validate encryption key
    var masterKey = Environment.GetEnvironmentVariable("ENCRYPTION_MASTER_KEY") ??
                   app.Configuration["Encryption:MasterKey"];

    if (string.IsNullOrEmpty(masterKey))
    {
        throw new InvalidOperationException("Encryption master key not configured");
    }

    // Security middleware
    if (!environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
    {
        // Skip HTTPS redirection when running in Docker
    }
    else
    {
        app.UseHttpsRedirection();
    }
    // Security headers middleware
    app.Use(async (context, next) =>
    {
        if (!environment.IsDevelopment())
        {
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Permissions-Policy", "camera=(self), microphone=(self), geolocation=()");

            var csp = app.Configuration["Security:ContentSecurityPolicy"];
            if (!string.IsNullOrEmpty(csp))
            {
                context.Response.Headers.Add("Content-Security-Policy", csp);
            }
        }

        await next();
    });

    app.UseStaticFiles();
    app.UseRouting();

    app.UseSession();
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    exception = x.Value.Exception?.Message,
                    duration = x.Value.Duration.ToString()
                })
            };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    });

    // SignalR Hubs
    app.MapHub<MentorshipChatHub>("/mentorshipChatHub");
    app.MapHub<ChatHub>("/chatHub");

    // Controller routes
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // Seed data
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine("=== DEBUG: DbContext Connection String ===");
            var contextConnectionString = context.Database.GetConnectionString();
            Console.WriteLine($"DbContext connection string: '{contextConnectionString}'");
            Console.WriteLine("=== END DbContext DEBUG ===");

            // Ensure database is created and migrated
            await context.Database.MigrateAsync();

            // Seed roles
            var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeederService>();
            await roleSeeder.SeedRolesAsync();

            // Seed admin user
            var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeederService>();
            await adminSeeder.SeedAsync();

            await Freelancing.SeedGoals.SeedGoalsData(context);
            await Freelancing.SeedUserSkills.SeedUserSkillsData(context);
            await Freelancing.SeedContractTemplates.SeedAsync(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    // Initialize Random Forest service
    await InitializeRandomForestServiceAsync(app.Services);

    // Register cleanup for PDF generation service
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        var pdfService = app.Services.GetService<IPdfGenerationService>();
        pdfService?.Dispose();
    });
}

static async Task InitializeRandomForestServiceAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    try
    {
        var randomForestService = scope.ServiceProvider.GetRequiredService<ILocalRandomForestService>();

        var initTask = randomForestService.EnsureInitializedAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));

        var completedTask = await Task.WhenAny(initTask, timeoutTask);

        if (completedTask == initTask)
        {
            await initTask;
            Log.Information("Random Forest service initialized successfully");
        }
        else
        {
            Log.Warning("Random Forest initialization timed out - will initialize on first use");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Random Forest initialization failed - will initialize on first use");
    }
}