use std::collections::BTreeMap;
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::Command;

use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Manager};

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TodoItemData {
    #[serde(default)]
    pub id: u32,
    #[serde(default)]
    pub text: String,
    #[serde(default)]
    pub note: String,
    #[serde(default)]
    pub owner: String,
    #[serde(default)]
    pub deadline: String,
    #[serde(default)]
    pub deadline_text: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TodoData {
    #[serde(default)]
    pub updated_at: u64,
    #[serde(default)]
    pub items: Vec<TodoItemData>,
    #[serde(default)]
    pub checked: BTreeMap<String, bool>,
    #[serde(default)]
    pub sort_by_deadline: bool,
}

fn app_data_dir(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_data_dir()
        .map_err(|e| format!("无法定位数据目录: {e}"))
}

fn data_file_path(app: &AppHandle) -> Result<PathBuf, String> {
    Ok(app_data_dir(app)?.join("data.json"))
}

fn ensure_dir(p: &Path) -> Result<(), String> {
    if let Some(dir) = p.parent() {
        fs::create_dir_all(dir).map_err(|e| format!("创建目录失败: {e}"))?;
    }
    Ok(())
}

#[tauri::command]
fn load_data(app: AppHandle) -> Result<Option<TodoData>, String> {
    let p = data_file_path(&app)?;
    if !p.exists() {
        return Ok(None);
    }
    let raw = fs::read_to_string(&p).map_err(|e| format!("读取数据文件失败: {e}"))?;
    match serde_json::from_str::<TodoData>(&raw) {
        Ok(d) => Ok(Some(d)),
        Err(e) => Err(format!("数据文件解析失败（可删除 {} 后重试）: {e}", p.display())),
    }
}

#[tauri::command]
fn save_data(app: AppHandle, data: TodoData) -> Result<(), String> {
    let p = data_file_path(&app)?;
    ensure_dir(&p)?;
    let json = serde_json::to_string_pretty(&data).map_err(|e| format!("序列化失败: {e}"))?;

    // 原子写：先写临时文件再改名；旧文件保留为 .bak
    let tmp = p.with_extension("json.tmp");
    let mut f = fs::File::create(&tmp).map_err(|e| format!("创建临时文件失败: {e}"))?;
    f.write_all(json.as_bytes())
        .map_err(|e| format!("写入失败: {e}"))?;
    f.sync_all().map_err(|e| format!("同步失败: {e}"))?;
    drop(f);

    if p.exists() {
        let bak = p.with_extension("json.bak");
        let _ = fs::copy(&p, &bak);
    }
    fs::rename(&tmp, &p).map_err(|e| format!("写入数据文件失败: {e}"))?;
    Ok(())
}

#[tauri::command]
fn data_path(app: AppHandle) -> Result<String, String> {
    Ok(data_file_path(&app)?.display().to_string())
}

#[tauri::command]
fn save_export(app: AppHandle, text: String) -> Result<String, String> {
    let dir = app_data_dir(&app)?;
    ensure_dir(&dir)?;
    let p = dir.join("制卡技术待办清单.txt");
    fs::write(&p, text).map_err(|e| format!("导出失败: {e}"))?;
    Ok(p.display().to_string())
}

#[tauri::command]
fn reveal_path(path: String) -> Result<(), String> {
    #[cfg(target_os = "macos")]
    let result = Command::new("open").arg("-R").arg(&path).status();
    #[cfg(target_os = "windows")]
    let result = Command::new("explorer").arg("/select,").arg(&path).status();
    #[cfg(target_os = "linux")]
    let result = Command::new("xdg-open").arg(&path).status();

    result
        .map(|_| ())
        .map_err(|e| format!("打开文件管理器失败: {e}"))
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            load_data,
            save_data,
            data_path,
            save_export,
            reveal_path
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}