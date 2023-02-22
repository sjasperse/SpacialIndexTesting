using SpacialIndexing.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GeoCityClient>();
builder.Services.AddHostedService( p => p.GetRequiredService<GeoCityClient>());

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseEndpoints(c => {
    c.MapControllers();
});

app.Run();
