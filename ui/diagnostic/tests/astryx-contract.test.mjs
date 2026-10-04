import assert from "node:assert/strict";
import test from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { Button } from "@astryxdesign/core/Button";
import { Theme } from "@astryxdesign/core/theme";
import { neutralTheme } from "@astryxdesign/theme-neutral/built";

test("the pinned Astryx theme renders an accessible, styled primary action", () => {
  const html = renderToStaticMarkup(React.createElement(
    Theme,
    { theme: neutralTheme },
    React.createElement(Button, {
      label: "Send",
      variant: "primary",
      className: "send-button",
      "aria-label": "Send message",
    }),
  ));

  assert.match(html, /<button\b/);
  assert.match(html, /astryx-button/);
  assert.match(html, /data-variant="primary"/);
  assert.match(html, /aria-label="Send message"/);
  assert.ok(html.includes(">Send<"), "the visible button label should be present inside Astryx markup");
});
