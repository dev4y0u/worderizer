
namespace WebApplication.Worderizer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            builder.Services.AddSwaggerDocument(c => { c.Title = typeof(Program).Namespace; });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            // Register Synfusion community license
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(SyncfusionLicense.LicenseKey);

            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.Run();
        }
    }
}