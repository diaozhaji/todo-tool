import { invoke } from "@tauri-apps/api/core";
import type { TodoData } from "../types";

export function loadDataCmd(): Promise<TodoData | null> {
  return invoke<TodoData | null>("load_data");
}

export function saveDataCmd(data: TodoData): Promise<void> {
  return invoke("save_data", { data });
}

export function getDataPathCmd(): Promise<string> {
  return invoke<string>("data_path");
}

export function saveExportCmd(text: string): Promise<string> {
  return invoke<string>("save_export", { text });
}

export function revealPathCmd(path: string): Promise<void> {
  return invoke("reveal_path", { path });
}