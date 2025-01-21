        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<BenchmarkService>();
            builder.Services.AddTransient<BenchmarkTests>();
