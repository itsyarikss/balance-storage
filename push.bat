@echo off
echo Pushing changes to GitHub...
git add .
git commit -m "Auto commit: %date% %time%"
git push origin master
echo Push completed.