export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-24">
      <h1 className="text-4xl font-bold tracking-tight">Welcome to Tenurix</h1>
      <p className="mt-4 text-lg text-gray-600">
        Find your perfect rental property.
      </p>
      <div className="mt-8 flex gap-4">
        <a href="/listings" className="rounded-lg bg-blue-600 px-6 py-3 text-white font-semibold hover:bg-blue-700">
          Browse Listings
        </a>
        <a href="/auth" className="rounded-lg border border-gray-300 px-6 py-3 font-semibold hover:bg-gray-50">
          Sign In
        </a>
      </div>
    </main>
  );
}
