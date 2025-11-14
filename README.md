# VUniBox Backend API

**Hệ thống quản lý tài liệu học thuật thông minh với AI**

## 📖 Mô tả dự án

VUniBox là nền tảng quản lý tài liệu học thuật được tích hợp AI, giúp sinh viên tổ chức tài liệu và tự động tạo trích dẫn chuẩn quốc tế. Dự án xây dựng RESTful API backend với ASP.NET Core 8.0, tích hợp Google Gemini 2.5 Pro để phân loại tài liệu, tạo citation tự động và chatbot hỗ trợ.

## 🛠️ Tech Stack

**Backend Framework:**
- ASP.NET Core 8.0 Web API
- Entity Framework Core 8.0 + SQL Server
- JWT Authentication + Google OAuth 2.0

**AI & Machine Learning:**
- Google Gemini 2.5 Pro API (classification, citation generation, chatbot)
- Custom prompt engineering cho tài liệu học thuật tiếng Việt

**Cloud & Payment:**
- Digital Ocean
- PayOS Payment Gateway (VN)

**Document Processing:**
- DocumentFormat.OpenXml (Word metadata)
- iTextSharp (PDF extraction)
- HtmlAgilityPack (URL scraping)

**Architecture Patterns:**
- Repository Pattern + Service Layer
- Dependency Injection
- Background Services (IHostedService)

## ✨ Key Features

- 🤖 AI-powered document classification (Research/Book/Newspaper/PDF/Word/Others)
- 📝 Auto-generate citations (APA, MLA, Chicago, Harvard, IEEE)
- 💬 AI chatbot for citation assistance
- 📦 Document management (upload, metadata extraction, trash system)
- 💳 Subscription plans with quota management (FREE/BASIC/PREMIUM)
- 🔐 Secure authentication (JWT + Google OAuth + Email OTP)
- 📊 Admin dashboard with analytics
- ⚙️ Background services (auto trash cleanup, monthly quota reset, subscription expiry)


**Tech Skills Demonstrated:** ASP.NET Core, EF Core, RESTful API Design, AI Integration, Cloud Services, Payment Gateway, Background Processing, Security Best Practices