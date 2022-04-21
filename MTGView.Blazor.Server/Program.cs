using MTGView.Blazor.Server.Bootstrapping;
using MTGView.Blazor.Server.Middleware;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithProcessName()
    .Enrich.WithEnvironmentUserName()
    .WriteTo.Async(a =>
    {
        a.File("./logs/log-.txt", rollingInterval: RollingInterval.Day);
        a.Console();
    })
    .CreateBootstrapLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host
        .ConfigureAppConfiguration((context, config) =>
        {
            config
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true)
                .AddEnvironmentVariables();
        })
        .UseDefaultServiceProvider(options => options.ValidateScopes = false)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services));

    builder.Logging
        .ClearProviders()
        .AddSerilog();

    // Add services to the container.
    builder.Services
        .AddBlazorise(options => { options.Immediate = true; })
        .AddBootstrap5Providers()
        .AddBootstrap5Components()
        .AddBootstrapIcons()
        .AddBlazoriseRichTextEdit();

    builder.Services.AddLazyCache();

    builder.Services.AddAutomappingProfiles<Program>();
    builder.Services.AddScoped<IModuleFactory, EsModuleFactory>();
    builder.Services.AddScoped<MtgIndexedDb>();
    builder.Services.AddScoped<SetInformationRepository>();
    builder.Services.AddScoped<SymbologyRepository>();

    builder.Services.AddResponseCompression(options =>
    {
        options.MimeTypes = new[] { System.Net.Mime.MediaTypeNames.Application.Octet };
    });

    builder.Services.AddResponseCaching();

    builder.Services.AddMtgDataServices(builder.Configuration.GetConnectionString("MtgDb"));

    builder.Services.AddPersonalCollectionServices(builder.Configuration.GetConnectionString("PersonalCollectionsDb"));

    builder.Services.AddScryfallApiServices();

    builder.Services.AddSignalR();

    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor()
        .AddHubOptions(options =>
        {
            options.MaximumReceiveMessageSize = 104_857_600;
        });

    var app = builder.Build();

    app.UseResponseCompression();

    // Configure the HTTP request pipeline.
    app.UseEnvironmentMiddleware(app.Environment);
    app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = RequestLoggingConfigurer.EnrichFromRequest);

    app.UseWebSockets();

    app.UseHttpsRedirection();

    app.UseStaticFiles();
    
    app.UseRouting();

    app.UseApiExceptionHandler(options =>
    {
        options.AddResponseDetails = OptionsDelegates.UpdateApiErrorResponse;
        options.DetermineLogLevel = OptionsDelegates.DetermineLogLevel;
    });

    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal("An error occured before {appName} could launch: {@ex}", nameof(MTGView), ex);
}
finally
{
    Log.CloseAndFlush();
}