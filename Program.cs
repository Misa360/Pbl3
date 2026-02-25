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
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
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
})
.AddGoogle(options =>
{
    // Cấu hình Google OAuth với ClientId và ClientSecret từ appsettings.json
    options.ClientId = builder.Configuration["Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    options.CallbackPath = "/signin-google";
});

// ─── 3. ĐĂNG KÝ SERVICES (Dependency Injection) ──────────────────────────────
// Khi controller cần IAuthService, ASP.NET tự inject AuthService vào
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IArticleService, ArticleService>();

// ─── 4. MVC + API ─────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
// Thêm cấu hình Session
builder.Services.AddDistributedMemoryCache(); // Đăng ký bộ nhớ đệm
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session tồn tại trong 30p
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

// Kích hoạt Session (Phải đặt trước Auth)
app.UseSession();

// Thứ tự quan trọng: Authentication trước, Authorization sau
app.UseAuthentication();
app.UseAuthorization();

// Route cho API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();