const apiOrigins = ["https://localhost:7182", "http://localhost:5265"];

export const apiFetch = async (path, options) => {
  let lastErr = null;
  for (const origin of apiOrigins) {
    try {
      const url = `${origin}${path.startsWith("/") ? path : `/${path}`}`;
      // debug: log attempted URL
      // eslint-disable-next-line no-console
      console.debug("apiFetch trying:", url);
      const res = await fetch(url, options);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      return res;
    } catch (err) {
      // eslint-disable-next-line no-console
      console.debug("apiFetch failed for origin", origin, err);
      lastErr = err;
      // try next origin
    }
  }
  throw lastErr;
};

export default apiFetch;
