# 先拉取 GitHub 上的更新，再推送（提交者仍是你本机 git 配置里的身份）
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

$name = git config --get user.name
$email = git config --get user.email
if (-not $name -or -not $email) {
    Write-Host "请先配置 user.name / user.email" -ForegroundColor Red
    exit 1
}

Write-Host "身份: $name <$email>" -ForegroundColor Cyan

# 若上一笔提交作者写成了 `$name` 字面量，可修正（仅当还没 push 成功时）
$lastAuthor = git log -1 --format="%an"
if ($lastAuthor -eq '$name') {
    Write-Host "正在修正上一笔提交的作者信息..." -ForegroundColor Yellow
    git -c "user.name=$name" -c "user.email=$email" commit --amend --author="$name <$email>" --no-edit
}

Write-Host "拉取远程 main（rebase）..." -ForegroundColor Cyan
git pull origin main --rebase

Write-Host "推送到 GitHub..." -ForegroundColor Cyan
git push origin main

Write-Host "完成。" -ForegroundColor Green
git log -1 --format="  %h %an <%ae> %s"
