# ChangeFlow

## معرفی و هدف پروژه

ChangeFlow یک HTTP API آموزشی برای مدیریت درخواست‌های تغییر نرم‌افزار است. این پروژه چرخه‌ای کنترل‌شده برای ایجاد، ویرایش، ارسال، بررسی و تغییر وضعیت درخواست‌ها فراهم می‌کند. در این پروژه دو نقش اصلی وجود دارد:

- **Requester**: درخواست تغییر ایجاد می‌کند، Draft و ChangesRequested را ویرایش می‌کند و درخواست را برای بررسی ارسال می‌کند.
- **Reviewer**: درخواست‌های PendingReview را بررسی می‌کند، تأیید می‌کند یا با یک Comment تغییرات لازم را درخواست می‌کند.

کاربر می‌تواند هر دو نقش را داشته باشد، اما اجازه ندارد درخواست خودش را تأیید کند.

## ویژگی‌ها

- ایجاد Change Request با عنوان، توضیح، دلیل و شناسه Requester
- چرخه وضعیت:
  - `Draft -> PendingReview` با Submit
  - `PendingReview -> Approved` با Approve
  - `PendingReview -> ChangesRequested` با RequestChanges
  - `ChangesRequested -> PendingReview` با ویرایش و Submit مجدد
- جلوگیری از ویرایش PendingReview، ویرایش یا بازگشایی Approved، و حذف Draft
- بررسی هویت، مجوز سرور و وضعیت فعلی درخواست برای عملیات تغییر وضعیت
- جلوگیری از خودتأییدی
- جلوگیری از overwrite همزمان با application-managed concurrency GUID
- پاسخ‌های problem/JSON برای خطاها و کدهای ثابت `MissingOrEmptyChangeComment`, `InvalidState`, `ConcurrencyConflict` و سایر موارد

## تکنولوژی‌ها و پیش‌نیازها

- .NET 10 و ASP.NET Core Web API با Controllers
- Entity Framework Core 10
- SQLite از طریق Microsoft.EntityFrameworkCore.Sqlite
- JWT Bearer Authentication برای احراز هویت
- xUnit و Microsoft.AspNetCore.Mvc.Testing برای تست‌های integration و HTTP

پیش‌نیازها:

- نصب .NET 10 SDK
- دسترسی به NuGet در زمان restore وابستگی‌ها
- SQLite بدون نیاز به سرویس پایگاه‌داده جداگانه

## ساختار Solution

```text
ChangeFlow.slnx
src/ChangeFlow.Api/
  Controllers/
  Data/
  Models/
  Services/
  Program.cs
  ChangeFlow.Api.csproj
tests/ChangeFlow.Api.Tests/
  Integration/
  Contract/
  Unit/
  ChangeFlow.Api.Tests.csproj
specs/001-manage-software-change/
  spec.md
  plan.md
  research.md
  data-model.md
  tasks.md
  contracts/openapi.yaml
  quickstart.md
```

معماری عمداً ساده است: یک پروژه API و یک پروژه تست، بدون Repository abstraction یا چندپروژه اضافی.

## اجرای API

از مسیر Repository اجرا کنید:

```powershell
dotnet restore
 dotnet run --project src/ChangeFlow.Api
```

در وضعیت Development، دیتابیس SQLite با نام `changeflow.db` در محیط پروژه ایجاد می‌شود و داده‌های Development از طریق seed قابل استفاده می‌شوند.

کلید امضای JWT نباید داخل Source Control قرار گیرد. آن را به‌صورت environment variable یا user-secret تنظیم کنید:

```powershell
dotnet user-secrets init --project src/ChangeFlow.Api
dotnet user-secrets set "DevelopmentJwt:SigningKey" "your-long-development-only-key" --project src/ChangeFlow.Api
dotnet user-secrets set "DevelopmentJwt:Issuer" "ChangeFlow.Development" --project src/ChangeFlow.Api
dotnet user-secrets set "DevelopmentJwt:Audience" "ChangeFlow.Api" --project src/ChangeFlow.Api
```

## احراز هویت Development و دریافت JWT

API یک endpoint فقط در Development دارد:

```text
POST /development/tokens
```

بدنه نمونه:

```json
{
  "userId": "10000000-0000-4000-8000-000000000001"
}
```

شناسه‌های seeded فعلی:

- Requester-only: `10000000-0000-4000-8000-000000000001`
- Reviewer-only: `20000000-0000-4000-8000-000000000002`
- Dual-role: `30000000-0000-4000-8000-000000000003`

پاسخ یک JWT کوتاه‌عمر شامل `accessToken` و `expiresAt` برمی‌گرداند. JWT فقط identity را تأمین می‌کند؛ مجوزهای Requester و Reviewer از دیتابیس خوانده می‌شوند و نقش‌های داخل token اعتبار مجوز سرور نیستند.

برای فراخوانی endpointهای محافظت‌شده، توکن را به‌صورت Header ارسال کنید:

```text
Authorization: Bearer <accessToken>
```

## اجرای تست‌ها

```powershell
dotnet test tests/ChangeFlow.Api.Tests/ChangeFlow.Api.Tests.csproj
```

آخرین اجرای ثبت‌شده از این README: **۱۲ تست اجرا شده، ۱۲ موفق، ۰ ناموفق**.

تست‌ها شامل سناریوهای ایجاد و ارسال، approval، request-changes، concurrency، رد تغییر Approved، حذف Draft، دیدن/ویرایش، لغو مجوز، کاربر Dual-role، و ترتیب mutation/revocation هستند.

## Workflow درخواست تغییر

```text
Draft
  -- Submit --> PendingReview
                    |
                    |-- Approve --> Approved
                    |
                    |-- RequestChanges(comment) --> ChangesRequested
                                                    |
                                                    -- Edit & Submit --> PendingReview
```

- تغییرات باید از طریق stateهای معین انجام شوند.
- `Reason` متعلق به Requester است و با comment Reviewer بازنویسی نمی‌شود.
- `LastChangeRequestComment` آخرین توضیح Reviewer است و با RequestChanges بعدی جایگزین می‌شود.
- هر mutation باید concurrency GUID مشاهده‌شده را ارسال کند و در صورت stale بودن با `409 ConcurrencyConflict` رد شود.

## محدودیت‌ها و موارد خارج از محدوده

- Frontend ندارد.
- ثبت‌نام کاربر، production identity provider، token refresh و account recovery پیاده‌سازی نشده‌اند.
- Permission revocation فعلاً capability برنامه نیست و فقط به‌صورت administrative/database خارج از API برنامه‌ریزی شده است.
