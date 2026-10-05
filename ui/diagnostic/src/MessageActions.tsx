import { useEffect, useRef, useState } from "react";
import { Button } from "@astryxdesign/core/Button";
import { copyVisibleMessageText } from "./message-copy.mjs";

export function MessageActions({ text, truncated, streaming }: { text: string; truncated?: boolean; streaming?: boolean }) {
  const [state, setState] = useState<"idle" | "copying" | "copied" | "failed">("idle");
  const generation = useRef(0);
  useEffect(() => {
    generation.current++;
    setState("idle");
    return () => { generation.current++; };
  }, [text]);
  async function copy() {
    const current = generation.current;
    setState("copying");
    try {
      await copyVisibleMessageText(text);
      if (generation.current === current) setState("copied");
    } catch {
      if (generation.current === current) setState("failed");
    }
  }
  return <span className="message-actions">
    <Button size="sm" variant="ghost" label={truncated ? "Copy visible preview" : "Copy message"}
      tooltip={truncated ? "Copies only the displayed text, not omitted source" : "Copy the message text"}
      isDisabled={!text || streaming || state === "copying"} onClick={() => void copy()} />
    {state === "copied" && <span role="status">Copied</span>}
    {state === "failed" && <span role="alert">Could not copy. Select the text and copy it manually.</span>}
  </span>;
}
