InfoTrack — Solicitors scraper (local workspace)
===============================================

Overview
--------
This repository implements the InfoTrack development task: a .NET 8 Web API + SPA that automates extraction of solicitors' contact details from a default source (https://www.solicitors.com/conveyancing.html) by location. The solution in this workspace is configured to run locally from source using Visual Studio.

What’s in this workspace
------------------------
- InfoTrack.Solicitors.Api — .NET 8 Web API that performs scraping, parsing and exposes REST endpoints.
- InfoTrack.Solicitors.Web — SPA frontend for configuring locations, running scrapes and viewing/exporting reports.
- InfoTrack.Solicitors.Api.Tests — unit tests for core parsing/business logic.

Quick prerequisites
-------------------
- .NET 8 SDK
- Node.js + npm (for SPA development, if you want to run the client separately)
- Visual Studio 2022/2026 (solution: InfoTrack.Solicitors.Api\InfoTrack.Solicitors.Api.slnx) or VS Code

Run locally (Visual Studio)
---------------------------
1. Open InfoTrack.Solicitors.Api\InfoTrack.Solicitors.Api.slnx in Visual Studio.
2. Set InfoTrack.Solicitors.Api as the startup project (and InfoTrack.Solicitors.Web if you want the client run from IDE).
3. Run the solution. The API will start on the configured port and (if included) Swagger will be available at /swagger.

Run locally (dotnet CLI)
------------------------
1. Backend:
   - cd InfoTrack.Solicitors.Api
   - dotnet restore
   - dotnet run
2. Frontend (if run separately):
   - cd InfoTrack.Solicitors.Web (look for a ClientApp folder)
   - npm install
   - npm start

Configuration
-------------
- appsettings.json (in InfoTrack.Solicitors.Api) contains CORS, scraper settings and ConnectionStrings.
- By default the solution uses an in-memory store. To use SQL Express or Postgres, update ConnectionStrings:DefaultConnection and set the provider in Program.cs.

API surface
-----------
- Endpoints let you: trigger a scrape for selected locations, retrieve latest results, retrieve historical results and export CSV/JSON reports. Exact controller routes are discoverable via Swagger or by inspecting the Api project controllers.

Notes about scraping
--------------------
- Default source: the application scrapes the conveyancing search at https://www.solicitors.com/conveyancing.html. The source URL is configurable so you can change or extend sources.
- The scraper was implemented without third-party HTML-parsing libraries (per task requirements) and uses structured parsing logic inside the Api project.
- Be respectful of site terms and robots.txt. The scraper includes sensible timeouts and simple rate-limiting; adjust settings in appsettings.json if needed.

Testing
-------
- Run unit tests from the solution root:
  - dotnet test

Data Storage
-------
- Data from snapshots / locations from local sessions is stored in %LOCALAPPDATA%\InfoTrack.Solicitors

Sample data / export
--------------------
- The application supports exporting results as JSON and Excel-compatible CSV. Exports are available from the UI and API.
- A sample export (solicitors-2026-07-27T22-18-15-741Z.json) was used during development but is not required in the repo; run a scrape to generate exports.

High-level features
-------------------
- Default source scraping (configurable) for conveyancing listings.
- Pagination enabled for results (set to 50)
- Manage location sets in the UI (add/remove locations) and run scrapes against those sets.
- Save scrape results and compare states when the same location set is used to detect new or removed solicitors.
- Export results to JSON or Excel/CSV from the UI or API.
- Edit and delete saved entries from history.
- View standard report layout (name, location, address, phone, email, source URL, scrape timestamp, notes).
- Simple in-memory store by default; optional persistent storage via SQL Express or Postgres.
 - Automatic ranking: when viewing results for selected locations, the UI computes and displays top solicitors at the top of the report (based on available heuristics such as listing prominence, ratings, or match quality).
