# Game-programming

This is a inventory for project

## Unity 工程（在本仓库里做）

1. **克隆仓库**到本机（若还没有）：
   `git clone https://github.com/Mashuyu916/Game-programming.git`
2. 打开 **Unity Hub** → **Add project from disk** 若已有完整工程；若是第一次，选 **New project** → **2D (Built-in 或 URP 均可)** → 位置选 **本仓库根目录**（与 `README.md` 同级）。  
   Unity 会生成 `ProjectSettings`、`Packages` 等；本仓库已包含 `Assets/Scripts/unity-2d-equipment/` 下的脚本，打开后会自动编译。
3. 根目录已有 **`.gitignore`**，不要提交 `Library/`、`Temp/` 等大文件夹。
4. 在 Unity 里按脚本注释搭建场景（地面 Layer、Player、`PlayerMovement2D`、`PlayerEquipmentCombat`、`WeaponData` 等）。详细步骤可参考协作仓库里的说明或自行扩展。

### 脚本位置

- `Assets/Scripts/unity-2d-equipment/` — 移动、武器数据、攻击判定、简单受击等示例代码。

### 推送

在本仓库目录执行：

```bash
git add .
git status
git commit -m "Describe your change"
git push origin main
```
