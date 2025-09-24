using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.OpenApi.Models;
using VUniBox.Services.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text; // Add this using directive at the top of the file
using VUniBox.Services.Classification; // Add this using directive
using VUniBox.Services.Citation; // Add this using directive
using VUniBox.Services.Chatbot; // Add this using directive
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

// Set console encoding to UTF-8 for proper Vietnamese character display
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// Register all required services for the application, including controllers, Swagger, authentication, and custom services.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to handle object cycles and UTF-8 encoding
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
        // Ensure proper Unicode encoding for Vietnamese characters
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Enable OpenAPI/Swagger for API documentation and testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v2", new OpenApiInfo
{
    Title = "VUniBox API",
    Version = "v2",
    //Description = "VUniBox API provides endpoints for generating educational slides and video lessons using AI. Use the endpoints below to create, manage, and retrieve educational content."
});
    // Add JWT Bearer authentication to Swagger UI so that protected endpoints can be tested.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// In development, load sensitive configuration from a separate secrets file.
// This allows you to keep secrets out of source control.
if (builder.Environment.IsDevelopment() || builder.Environment.IsProduction())
{
    builder.Configuration.AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);
}

// Register other application services for dependency injection.
builder.Services.AddHttpClient();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IJwtService, JwtService>();  


// Register Gemini Classification Service with HttpClient
builder.Services.AddHttpClient<IGeminiClassificationService, GeminiClassificationService>();

// Register Gemini Citation Service with HttpClient
builder.Services.AddHttpClient<IGeminiCitationService, GeminiCitationService>();

// Register Gemini Chatbot Service with HttpClient
builder.Services.AddHttpClient<IGeminiChatbotService, GeminiChatbotService>();

// Register Session for chat history storage
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2); // Session expires after 2 hours of inactivity
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "VUniBox.Session";
});

// Register Session Chat Service
builder.Services.AddScoped<SessionChatService>();
builder.Services.AddHttpContextAccessor();

// Register Citation Management Service
builder.Services.AddScoped<ICitationManagementService, CitationManagementService>();

// Register Classification Services
builder.Services.AddScoped<VUniBox.Services.Classification.IFileClassificationService, VUniBox.Services.Classification.FileClassificationService>();
builder.Services.AddScoped<VUniBox.Services.Classification.IUrlClassificationService, VUniBox.Services.Classification.UrlClassificationService>();
builder.Services.AddScoped<VUniBox.Services.Classification.IClassificationService, VUniBox.Services.Classification.ClassificationService>();

// Register Metadata Extraction Services
builder.Services.AddScoped<VUniBox.Services.Metadata.IFileMetadataExtractor, VUniBox.Services.Metadata.FileMetadataExtractor>();
builder.Services.AddScoped<VUniBox.Services.Metadata.IUrlMetadataExtractor, VUniBox.Services.Metadata.UrlMetadataExtractor>();
builder.Services.AddScoped<VUniBox.Services.Metadata.IMetadataExtractionService, VUniBox.Services.Metadata.MetadataExtractionService>();

// Register Document Management Services
// Register Document Management Service
builder.Services.AddScoped<VUniBox.Services.DocumentManagement.IDocumentLifecycleService, VUniBox.Services.DocumentManagement.DocumentLifecycleService>();

// Register Usage Tracking Service
builder.Services.AddScoped<VUniBox.Services.Usage.IUsageTrackingService, VUniBox.Services.Usage.UsageTrackingService>();

// Register Quota Management Service
builder.Services.AddScoped<VUniBox.Services.Quota.IQuotaManagementService, VUniBox.Services.Quota.QuotaManagementService>();

// Register Subscription Service
builder.Services.AddScoped<VUniBox.Services.Subscription.ISubscriptionService, VUniBox.Services.Subscription.SubscriptionService>();

// Register PayOS Payment Service
builder.Services.AddScoped<VUniBox.Services.Payment.IPayOSService, VUniBox.Services.Payment.PayOSService>();

builder.Services.AddHostedService<VUniBox.Services.Background.TrashCleanupService>();
builder.Services.AddHostedService<VUniBox.Services.Background.MonthlyQuotaResetService>();

// Add this before app.Build();
builder.Services.AddDbContext<VUniBox.DBContext.VUniBoxContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT authentication for securing API endpoints.
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Enable authorization policies for role-based access control.
builder.Services.AddAuthorization();

// Configure CORS to allow requests from the frontend application.
// This is necessary for browser-based clients to interact with the API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("*"));
    
    // Add specific policy for development
    options.AddPolicy("Development",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("*"));
});


var app = builder.Build();

// Configure different behavior for development vs production
if (app.Environment.IsDevelopment())
{
    // Enable Swagger UI for API exploration and testing.
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "EduVision API v2");
        // c.RoutePrefix = string.Empty; // Uncomment to serve at root
    });
    
    // Use more permissive CORS in development
    app.UseCors("Development");
}
else
{
    // Production configuration
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "EduVision API v2");
    });
    
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();

app.UseSession(); // Enable session middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
