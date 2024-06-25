using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReviewApi.Middleware;
using ReviewApi.Services;
using StackExchange.Redis;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder.WithOrigins("https://your-website.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

builder.Services.AddSingleton<ReviewService>();
builder.Services.AddHttpClient<ImageService>(); 
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// Configure the HTTP request pipeline.
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

//app.UseCors("CorsPolicy");

app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Initialize MongoDB indexes
var scope = app.Services.CreateScope();
var reviewService = scope.ServiceProvider.GetRequiredService<ReviewService>();
// Ensure indexes or any other initialization logic
await reviewService.GetReviewsByProductIdAsync("test"); // This will trigger the index creation if it doesn't exist

app.Run();
