# Spec-Driven Development در پروژه ChangeFlow

این سند شرحی آموزشی از فرایند واقعی طی‌شده برای پروژه ChangeFlow است. هدف Spec-Driven Development این است که تصمیمات محصول، طراحی و پیاده‌سازی در یک زنجیره قابل‌ردگیری قرار گیرند؛ نه اینکه فقط کد بدون سند نوشته شود.

## 1. Constitution

ابتدا Constitution پروژه در `.specify/memory/constitution.md` تعریف شد. این فایل اصول غیرقابل‌چانه مانند ساده‌سازی، تمامیت workflow، تفکیک نقش‌ها، integrity داده، تست اتوماتیک و traceability را تثبیت کرد.

چرایی این مرحله: قبل از نوشتن feature، قواعد ثابت پروژه تعیین می‌شوند تا در مراحل بعدی هر تصمیم با آن‌ها مقایسه شود. برای این پروژه، اصول مهمی مانند جلوگیری از خودتأییدی، رد تغییرات partial، concurrency بررسی‌پذیر و عدم ادعای اعتبار فقط با compile شدند.

نتیجه واقعی این مرحله یک Constitution نسخه 1.0.0 بود. این مرحله برنامه، مسیرها یا source code را پیاده‌سازی نکرد؛ فقط مرجع حکمرانی پروژه را ایجاد کرد.

## 2. Specify و Clarify

در مرحله Specify، یک Feature Specification برای مدیریت Change Request ایجاد شد. این spec وضعیت‌های Draft، PendingReview، ChangesRequested و Approved و transitionهای مجاز را مشخص کرد. همچنین roles، entities، user stories و success criteria را از implementation implementation جدا نگه داشت.

این مرحله «چه چیزی ساخته شود» را توضیح داد؛ نه «چه زبان یا دیتابیسی استفاده شود».

سپس Clarify سوال‌های بااهمیت را به‌صورت گزینشی مطرح کرد و پاسخ‌های واقعی را در بخش Clarifications ثبت کرد. به‌طور نمونه، scope Reviewer، visibility، الزام comment، رفتار PendingReview، revocation permission، بازگشایی Approved و حذف Draft روشن شدند. سوال‌هایی که پاسخ قطعی پروژه‌ای نداشتند در spec باقی ماندند.

هنوز یک ambiguity عمده باقی است: Reviewer visibility برای states خارج از PendingReview در این پیاده‌سازی مشخص نشده و نباید برای آن policy اختیاری فرض شود.

## 3. Plan و Research

مرحله Plan از روی spec و constitution یک implementation plan ساخت. این plan .NET 10، ASP.NET Core Controllers، Entity Framework Core و SQLite را برای scale آموزشی انتخاب کرد.

Research نقش مهمی داشت چون چند تصمیم باید روشن می‌شد:

- چرا SQLite انتخاب شد و چه trade-offهایی دارد
- تفاوت authentication و authorization
- استفاده از JWT bearer validation و یک issuer محدود Development
- استفاده از application-managed GUID به جای rowversion دیتابیسی
- coordination Permission و mutation با immediate transaction
- نگاشت response و error codeها

نتیجه این بود که implementation از فناوری‌های مشخص، اما از محصول یا behavior فرضی جدا باقی ماند. Product ambiguities باید از spec بودند، نه اینکه در Research حل شوند.

## 4. Tasks

مرحله Tasks این تصمیم‌ها را به کارهای قابل اجرا تقسیم کرد. Taskها بر پایه User Story سازماندهی شدند و مراحل Setup، Foundation، User Storyها و Polish را تشکیل دادند.

هر task مسئولیت مشخصی داشت: ایجاد پروژه، entityها، DbContext، authentication، workflow، controllerها، تست‌ها و validation. این مرحله کمک کرد implementation به‌جای یک تغییر بزرگ، به incrementهای مستقل تبدیل شود.

نکته مهم tasks.md این بود که تمام constrained fields و کارهای bounded از data-model و plan باید traceable باشند. هر task phase و endpoint یا file مشخصی را هدف می‌گیرد.

## 5. Analyze

بعد از ایجاد Tasks، Analyze برای بررسی سازگاری spec، plan، tasks و constitution اجرا شد. هدف این نبود که آزمون را جایگزین کند؛ بلکه inconsistencyها، duplicationها، coverage gapها و citationهای data-model را پیدا کند.

تحلیل‌های واقعی نشان دادند:

- Draft deletion ابتدا پوشش صریح لازم داشت؛ سپس task تست و task implementation اضافه شد.
- برخی Permission fields نیاز به canonical field citation داشتند.
- retry SQLite و timeout باید concrete تعریف شوند.
- Revoker visibility خارج از PendingReview نباید حل یا فرض شود.

این مرحله قبل از implementation می‌تواند خطاهای مستندات را پیدا کند و از پیاده‌سازی ناسازگار جلوگیری کند.

## 6. Implement

مرحله Implement پروژه ASP.NET Core را scaffold کرد و workflow را بر پایه artifacts پیاده‌سازی کرد. implementation شامل:

- EF Core entities و unique permission index
- SQLite transaction با نویزساز immediate
- application-managed concurrency GUID
- JWT Development issuer
- Controllerهای create/get/edit/submit/approve/request-changes
- problem/JSON response mapping
- تست‌های integration و workflow

نتیجه verify شده: build پروژه موفق بود و آخرین اجرای تست‌ها ۱۲ تست موفق و صفر ناموفق داشت.

بعضی موارد محدودیت محصول دارند. تست مستقل Dual-role و دو سناریوی ordering revoke/mutation در اجرای نهایی موجود و موفق هستند. Scope دید Reviewer برای states خارج PendingReview همچنان unresolved باقی مانده و نباید با این تست‌ها حل‌شده فرض شود.

## 7. Converge

Converge موجود بودن implementation را با spec، plan و tasks مقایسه می‌کند. نه یک diff Git و نه جایگزین implementation است؛ بلکه gapها را پیدا می‌کند و taskهای remaining را به tasks.md اضافه می‌کند.

در دو مرور Convergence واقعی، موارد زیر پیدا و به‌عنوان task追加 شدند:

- Permission revocation/restoration باید از transaction path مشترک استفاده کند.
- Reviewer visibility فقط برای scope تاییدشده PendingReview محدود شد؛ states خارج از آن همچنان unresolved هستند.
- limit 200 کاراکتر برای Title که در artifacts نبود حذف شد.

Templateهای WeatherForecast و UnitTest1 نیز در این فرایند حذف شدند تا scope پروژه خاموش‌ماند و ساده بماند.

## درس‌های اصلی

### Specification کیفیت را قبل از کدنویسی تثبیت می‌کند

بدون spec و constitution، تصمیم‌های ضمنی در کد پراکنده می‌شوند و later testing فقط implementation را تایید می‌کند، نه intent صحیح را.

### Clarify نباید ambiguityهای مهم را کم‌رنگ کند

Clarify چیزی که واقعاً unresolved است را نباید تبدیل به پاسخ فرضی کند. در این پروژه، Scope Reviewer خارج از PendingReview دقیقاً همین باقی مانده است.

### Plan باید technology-aware باشد، نه implementation

Plan explains choices and trade-offs. Implementation details belong in the code and task execution, but the plan establishes constraints such as SQLite immediate transaction, GUID concurrency, and JWT development issuer.

### Tasks chainها را قابل اجرا می‌کند

Taskها فایل‌ها، ستون‌ها، tests، endpoints و dependencies را traceable می‌کنند. این موضوع برای implementation و follow-up converge ضروری است.

### Analyze و Converge مکمل‌اند

Analyze قبل از implementation بین artifacts سازگاری می‌گیرد. Converge بعد از implementation gapهای کد نسبت به intent را پیدا می‌کند.

### Validation نیاز به evidence دارد

در این پروژه، Build موفق و ۱۲ Test موفق ثبت شده‌اند. با این حال، ادامه‌شدن taskهای unresolved یا gapهای جدید نباید به‌عنوان validated اجرا شود؛ فقط implement و run test می‌تواند آن‌ها را ببندد.

## وضعیت فعلی آموزشی

فرایند phases اساساً طی شده و implementation فعلی برای Product scope اصلی وجود دارد. تنها ambiguity محصولی منتشرنشده، Scope دید Reviewer در states خارج از PendingReview است.
