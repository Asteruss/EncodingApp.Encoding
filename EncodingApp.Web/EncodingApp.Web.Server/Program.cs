using EncodingApp.Encoding.Analysis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

builder.Services.AddControllers();

builder.Services.AddScoped<CompositeAnalyzer>(sp =>
    new CompositeAnalyzer(new TimingAnalyzer(), new CompressionRatioAnalyzer(), new GcPressureAnalyzer()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();