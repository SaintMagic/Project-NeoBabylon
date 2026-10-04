import { StrictMode, useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { Theme } from "@astryxdesign/core/theme";
import { neutralTheme } from "@astryxdesign/theme-neutral/built";
import "@astryxdesign/core/reset.css";
import "@astryxdesign/core/astryx.css";
import "@astryxdesign/theme-neutral/theme.css";
import "@fontsource-variable/dm-sans";
import "@fontsource-variable/manrope";
import App from "./App";
import { applyAppearance, readAppearance, saveAppearance, type Appearance } from "./appearance.mjs";
import { requestHost } from "./bridge";

const root = document.getElementById("root");
if (!root) throw new Error("NeoBabylon UI root element is missing.");

const initialAppearance = readAppearance();
applyAppearance(document.documentElement, initialAppearance);

function NeoBabylonRoot() {
  const [appearance, setAppearance] = useState<Appearance>(initialAppearance);
  useEffect(() => {
    void requestHost("setAppearance", { appearance }).then((result) => {
      if (result.appearance !== appearance) throw new Error("The desktop title bar did not confirm the selected appearance.");
    }).catch((error: unknown) => console.error("NeoBabylon could not synchronize the desktop title bar:", error));
  }, [appearance]);
  function changeAppearance(nextAppearance: Appearance) {
    setAppearance(nextAppearance);
    applyAppearance(document.documentElement, nextAppearance);
    saveAppearance(nextAppearance);
  }
  return <Theme theme={neutralTheme} mode={appearance}><App appearance={appearance} onAppearanceChange={changeAppearance} /></Theme>;
}

createRoot(root).render(<StrictMode><NeoBabylonRoot /></StrictMode>);
