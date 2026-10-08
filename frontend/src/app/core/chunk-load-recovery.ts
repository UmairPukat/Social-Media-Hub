const RELOAD_KEY = 'socialhub:stale-chunk-reload';

export function isChunkLoadError(error: unknown): boolean {
  const parts: string[] = [];
  if (error instanceof Error) {
    parts.push(error.name, error.message);
    const cause = (error as Error & { cause?: unknown }).cause;
    if (cause instanceof Error) {
      parts.push(cause.message);
    }
  } else if (error) {
    parts.push(String(error));
  }

  return /Failed to fetch dynamically imported module|error loading dynamically imported module|Loading chunk [\w.-]+ failed|ChunkLoadError/i
    .test(parts.join(' '));
}

export function reloadOnceOnStaleChunk(): boolean {
  try {
    if (sessionStorage.getItem(RELOAD_KEY) === '1') {
      sessionStorage.removeItem(RELOAD_KEY);
      return false;
    }
    sessionStorage.setItem(RELOAD_KEY, '1');
  } catch {
    // Ignore storage failures (private mode) and still reload once.
  }

  location.reload();
  return true;
}

export function installChunkLoadRecovery(): void {
  window.addEventListener('unhandledrejection', event => {
    if (!isChunkLoadError(event.reason)) return;
    event.preventDefault();
    reloadOnceOnStaleChunk();
  });

  window.addEventListener('error', event => {
    if (!isChunkLoadError(event.error) && !isChunkLoadError(event.message)) return;
    event.preventDefault();
    reloadOnceOnStaleChunk();
  });
}
