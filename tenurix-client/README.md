# Tenurix Client

A Next.js property management platform.

## Getting Started

```bash
npm install
npm run dev
```

## Environment Variables

Create a `.env.local` file:

```
NEXT_PUBLIC_API_BASE_URL=https://localhost:7001
```

## Project Structure

- `src/app/` — Pages and routes
- `src/components/` — Reusable UI components
- `src/lib/` — Utilities and API client

## Features

- Property listings with search and filters
- Tenant dashboard
- Landlord portal

## Authentication

Uses JWT tokens stored in localStorage. Supports email/password and Google OAuth.
