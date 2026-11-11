import { StrictMode, Component, type ErrorInfo, type ReactNode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";
import "./Experience.css";
import "./WaahRe.css";
import "./ReviewFlow.css";

class RenderErrorBoundary extends Component<
  { children: ReactNode },
  { hasError: boolean; message: string }
> {
  state = { hasError: false, message: "" };

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, message: error.message };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Waah रे Resume render error:", error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return (
        <main style={{ color: "white", padding: 48, fontFamily: "sans-serif" }}>
          <h1>RoleFit AI encountered a frontend error.</h1>
          <p>{this.state.message}</p>
          <p>Open F12 → Console for the full error details.</p>
          <button onClick={() => window.location.reload()}>Reload</button>
        </main>
      );
    }
    return this.props.children;
  }
}

const root = document.getElementById("root");
if (!root) throw new Error("React root element is missing.");
createRoot(root).render(
  <StrictMode>
    <RenderErrorBoundary>
      <App />
    </RenderErrorBoundary>
  </StrictMode>
);
