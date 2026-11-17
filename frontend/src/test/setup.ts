import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { setAccessToken } from "../services/api";
afterEach(() => {
  cleanup();
  setAccessToken(null);
});
Object.defineProperty(HTMLDialogElement.prototype, "showModal", {
  value: function (this: HTMLDialogElement) {
    this.setAttribute("open", "");
  },
  configurable: true,
});
Object.defineProperty(HTMLDialogElement.prototype, "close", {
  value: function (this: HTMLDialogElement) {
    this.removeAttribute("open");
  },
  configurable: true,
});
Object.defineProperty(window, "matchMedia", {
  value: vi.fn(() => ({
    matches: false,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  })),
  configurable: true,
});
