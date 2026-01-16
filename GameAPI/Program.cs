using api.Data;
using api.Models;
using api.DTOs;
using api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// MySQL 연결 설정
var connectionString = "Server=localhost;Database=TestDB;User=appuser;Password=1234;SslMode=None;";

// 설정한 MySQL DB 연결
builder.Services.AddDbContext<ApplicationDBContext>(o =>
    o.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// CORS (Unity 접근 허용)
builder.Services.AddCors(p => p.AddPolicy("UnityClient",
    policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Controller 사용 설정
builder.Services.AddControllers();

// JwtService 등록은 Build() 전에 해야 함
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<AuthService>();

// JWT 인증 설정
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("Jwt:Key 설정 누락"));

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),

        // 토큰 만료 여유시간 없애기 (보안 강화)
        ClockSkew = TimeSpan.Zero
    };
    options.SaveToken = true;
});

builder.Services.AddAuthorization();

// 앱 빌드는 모든 서비스 등록이 끝난 후에
var app = builder.Build();

// DB 없으면 자동 생성
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    db.Database.EnsureCreated();
}

app.UseCors("UnityClient");

// 1. 신분 확인
app.UseAuthentication();

// 2. 권한 확인
app.UseAuthorization();

app.MapControllers();
app.Run();
