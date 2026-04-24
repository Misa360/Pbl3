using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DaNangSafeMap.Data;
using DaNangSafeMap.Repositories;
using DaNangSafeMap.Services.Implementations;
using DaNangSafeMap.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. KẾT NỐI MYSQL ────────────────────────────────────────────────────────
// Đọc connection string từ appsettings.json và kết nối MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 31))
    )
);

// ─── 2. JWT AUTHENTICATION ────────────────────────────────────────────────────
// Cấu hình xác thực bằng JWT Bearer token
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
        )
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.ContainsKey("jwtToken"))
            {
                context.Token = context.Request.Cookies["jwtToken"];
            }
            return Task.CompletedTask;
        }
    };
});

// ─── 3. ĐĂNG KÝ SERVICES (Dependency Injection) ──────────────────────────────
// Khi controller cần IAuthService, ASP.NET tự inject AuthService vào
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ─── 3b. ALERT SERVICES (Bản đồ + Báo cáo sự cố) ────────────────────────────
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IAlertService, AlertService>();

// ─── 3c. ARTICLE SERVICES (Tin tức) ─────────────────────────────────────────
builder.Services.AddScoped<IArticleService, ArticleService>();

// In-memory cache for hot article queries (featured/latest/most-viewed/categories)
// Giảm tải DB trên trang Tin tức — TTL ngắn, tự invalidate khi duyệt / đăng bài.
builder.Services.AddMemoryCache();

// Pre-warm the article cache on app startup so the first visitor to /Article
// doesn't wait for the cold DB round-trip.
builder.Services.AddHostedService<DaNangSafeMap.Services.Implementations.ArticleCacheWarmer>();

// ─── 4. MVC + API ─────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// Cho phép API trả về JSON
builder.Services.AddEndpointsApiExplorer();

// ─── 5. CORS (cho phép frontend gọi API) ─────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ─── MIDDLEWARE PIPELINE ──────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Tắt tạm thời trên local để tránh redirect chậm nếu dùng http://
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

// Thứ tự quan trọng: Authentication trước, Authorization sau
app.UseAuthentication();
app.UseAuthorization();

// Route cho API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
