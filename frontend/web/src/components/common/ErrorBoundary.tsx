import React from 'react';

/**
 * Catches runtime errors in any subtree so one crashing page doesn't blank
 * the whole app. Renders a fallback with the error message + a reload button.
 *
 * Wrapped around every <Route> element in App.tsx so a single "Cannot read
 * properties of undefined" does not render a blank white screen — the user
 * sees the error, the nav stays visible, and can navigate elsewhere or
 * reload that route only.
 */

type Props = { children: React.ReactNode; routeLabel?: string };
type State = { error: Error | null };

class ErrorBoundary extends React.Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    // eslint-disable-next-line no-console
    console.error('LON ErrorBoundary caught:', error, info);
  }

  reset = () => this.setState({ error: null });

  render() {
    const { error } = this.state;
    if (!error) return this.props.children;

    return (
      <div style={{ padding: 30, maxWidth: 720, margin: '40px auto' }}>
        <div
          style={{
            border: '1px solid #e0a0a0',
            background: '#fff5f5',
            borderRadius: 8,
            padding: 24,
          }}
        >
          <h2 style={{ margin: '0 0 10px 0', color: '#b00020' }}>
            ⚠️ Страницата падна
            {this.props.routeLabel && (
              <span style={{ color: '#666', fontWeight: 400, fontSize: 14, marginLeft: 10 }}>
                ({this.props.routeLabel})
              </span>
            )}
          </h2>
          <p style={{ color: '#555', fontSize: 13, margin: '0 0 15px 0' }}>
            Нешто не е во ред со оваа страница. Може да се вратиш на друга преку менито
            лево, или пробај refresh.
          </p>
          <details style={{ background: '#fff', padding: 12, borderRadius: 4, border: '1px solid #eee' }}>
            <summary style={{ cursor: 'pointer', fontSize: 12, color: '#666' }}>
              Детали за програмер
            </summary>
            <pre style={{ fontSize: 11, marginTop: 10, whiteSpace: 'pre-wrap', color: '#b00020' }}>
              {error.name}: {error.message}
              {'\n'}
              {error.stack}
            </pre>
          </details>
          <div style={{ marginTop: 20, display: 'flex', gap: 10 }}>
            <button
              onClick={this.reset}
              style={{
                padding: '8px 14px',
                border: '1px solid #ccc',
                background: '#fff',
                borderRadius: 4,
                cursor: 'pointer',
              }}
            >
              Пробај повторно
            </button>
            <button
              onClick={() => window.location.reload()}
              style={{
                padding: '8px 14px',
                border: '1px solid #0066cc',
                background: '#0066cc',
                color: '#fff',
                borderRadius: 4,
                cursor: 'pointer',
              }}
            >
              Refresh страница
            </button>
          </div>
        </div>
      </div>
    );
  }
}

export default ErrorBoundary;
