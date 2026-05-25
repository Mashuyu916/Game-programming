# Run this yourself in PowerShell so the commit is 100% yours (not Cursor).
# Usage: right-click -> Run with PowerShell, or:
#   cd "C:\Users\rampa\My project (1)"
#   .\scripts\push-as-you.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

$name = git config --get user.name
$email = git config --get user.email
if (-not $name -or -not $email) {
    Write-Host "请先配置 Git 用户名（只改你自己电脑上的配置）：" -ForegroundColor Yellow
    Write-Host '  git config --global user.name "你的名字"'
    Write-Host '  git config --global user.email "你的邮箱"'
    exit 1
}

Write-Host "将使用你的身份提交: $name <$email>" -ForegroundColor Cyan

git add -A
$status = git status --porcelain
if (-not $status) {
    Write-Host "没有需要提交的改动。" -ForegroundColor Yellow
    git push origin main
    exit 0
}

git status -sb
$msg = @"
feat: custom map paint + invisible walk tile layer

- Visual tilemaps are decoration only (no collision)
- TilemapWalkable: paint InvisibleWalk tiles yourself
- Scene gizmos to preview invisible tiles in editor
"@

git -c "user.name=$name" -c "user.email=$email" commit -m $msg
Write-Host "提交完成:" -ForegroundColor Green
git -c "user.name=$name" -c "user.email=$email" log -1 --format="  %h %an <%ae> %s"

Write-Host "推送到 GitHub..." -ForegroundColor Cyan
git push origin main
Write-Host "完成: https://github.com/Mashuyu916/Game-programming" -ForegroundColor Green
