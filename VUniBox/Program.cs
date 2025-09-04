using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.OpenApi.Models;
using VUniBox.Services.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text; // Add this using directive at the top of the file
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

// Register all required services for the application, including controllers, Swagger, authentication, and custom services.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to handle object cycles
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
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
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);
}

// Register other application services for dependency injection.
builder.Services.AddHttpClient();
builder.Services.AddScoped<JwtService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Register Classification Services
builder.Services.AddScoped<VUniBox.Services.Classification.IFileClassificationService, VUniBox.Services.Classification.FileClassificationService>();
builder.Services.AddScoped<VUniBox.Services.Classification.IUrlClassificationService, VUniBox.Services.Classification.UrlClassificationService>();
builder.Services.AddScoped<VUniBox.Services.Classification.IClassificationService, VUniBox.Services.Classification.ClassificationService>();

// Register Metadata Extraction Services
builder.Services.AddScoped<VUniBox.Services.Metadata.IFileMetadataExtractor, VUniBox.Services.Metadata.FileMetadataExtractor>();
builder.Services.AddScoped<VUniBox.Services.Metadata.IUrlMetadataExtractor, VUniBox.Services.Metadata.UrlMetadataExtractor>();
builder.Services.AddScoped<VUniBox.Services.Metadata.IMetadataExtractionService, VUniBox.Services.Metadata.MetadataExtractionService>();

// Register Document Management Services
builder.Services.AddScoped<VUniBox.Services.DocumentManagement.IDocumentLifecycleService, VUniBox.Services.DocumentManagement.DocumentLifecycleService>();

// Register Background Services
builder.Services.AddHostedService<VUniBox.Services.Background.TrashCleanupService>();

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
            .AllowAnyMethod());
});


var app = builder.Build();

// Enable Swagger UI for API exploration and testing.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v2/swagger.json", "EduVision API v2");
    // c.RoutePrefix = string.Empty; // Uncomment to serve at root
});

app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
