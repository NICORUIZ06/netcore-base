﻿using Core;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class ServiceCollectionExtension
    {
        public static IConfiguration Configuration { get; set; }

        public static IServiceCollection RegisterInfrastructureDependencies(this IServiceCollection services)
        {
            var dbProvider = Configuration.GetValue<DbProvider>("DbProvider");
            switch (dbProvider)
            {
                case DbProvider.SqlServer:
                    ConfigureForSqlServer(services);
                    break;
                case DbProvider.InMemory:
                    ConfigureForInMemory(services);
                    break;
                case DbProvider.MySQL:
                    ConfigureForMySQL(services);
                    break;                
                default:
                    ConfigureForSqlServer(services);
                    break;
            }

            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped(typeof(IFileBuilder<>), typeof(FileBuilder<>));
            services.AddScoped<IMailProviderHelper, MailProviderHelper>(serviceProvider => BuildMailProvider());

            return services;
        }

        private static void ConfigureForMySQL(IServiceCollection services)
        {
            services.AddDbContext<DataContext>(options => options.UseMySQL(Configuration.GetConnectionString("DefaultConnection")));
        }

        private static void ConfigureForInMemory(IServiceCollection services)
        {
            services.AddDbContext<DataContext>(options => options.UseInMemoryDatabase(Configuration.GetConnectionString("DefaultConnection")));
        }

        private static void ConfigureForSqlServer(IServiceCollection services)
        {
            if (!string.IsNullOrEmpty(Configuration.GetConnectionString("DefaultConnection")))
            {
                services.AddDbContext<DataContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"), x => x.EnableRetryOnFailure()));
            }
            else
            {
                // For development usage only
                services.AddDbContext<DataContext>(options => options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SomeProject;Trusted_Connection=True;MultipleActiveResultSets=true"));
            }
        }

        public static void UpdateDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices
                .GetRequiredService<IServiceScopeFactory>()
                .CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<DataContext>())
                {
                    if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                    {
                        context.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` ( `MigrationId` nvarchar(150) NOT NULL, `ProductVersion` nvarchar(32) NOT NULL, PRIMARY KEY (`MigrationId`) );");
                        context.Database.Migrate();
                    }

                    context.Database.EnsureCreated();
                }
            }
        }

        private static MailProviderHelper BuildMailProvider()
        {
            string senderName = Configuration.GetValue<string>("EmailConfiguration:SenderName");
            string senderEmail = Configuration.GetValue<string>("EmailConfiguration:SenderEmail");
            string smtpHost = Configuration.GetValue<string>("EmailConfiguration:SmtpHost");
            int smtpPort = Configuration.GetValue<int>("EmailConfiguration:SmtpPort");
            string smtpUserName = Configuration.GetValue<string>("EmailConfiguration:SmtpUserName");
            string smtpPassword = Configuration.GetValue<string>("EmailConfiguration:SmtpPassword");
            bool useSsl = Configuration.GetValue<bool>("EmailConfiguration:UseSsl");

            return new MailProviderHelper(new SmtpClient(), senderName, senderEmail, smtpHost, smtpPort, smtpUserName, smtpPassword, useSsl);
        }
    }
}
