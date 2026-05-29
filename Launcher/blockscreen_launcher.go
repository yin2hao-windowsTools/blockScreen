//go:build windows

package main

import (
	"bytes"
	"context"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	appPayloadExe = "app\\blockScreen.App.exe"

	messageBoxYesNo        = 0x00000004
	messageBoxOK           = 0x00000000
	messageBoxIconWarning  = 0x00000030
	messageBoxIconError    = 0x00000010
	messageBoxDefaultFirst = 0x00000000
	messageBoxTopMost      = 0x00040000
	idYes                 = 6
	swShowNormal          = 1
)

var (
	appName              = "blockScreen"
	requiredRuntimeName  = "Microsoft.WindowsDesktop.App"
	requiredRuntimeMajor = "8"
	requiredArchitecture = "x64"
	runtimeDownloadURL   = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
	appVersion           = "dev"
	versionPattern       = regexp.MustCompile(`^` + regexp.QuoteMeta(requiredRuntimeMajor) + `\.`)
)

func main() {
	if !hasRequiredRuntime() {
		if askToOpenRuntimeDownloadPage() {
			openURL(runtimeDownloadURL)
		}

		return
	}

	payloadPath, err := resolvePayloadPath()
	if err != nil {
		showError("启动失败", err.Error())
		return
	}

	cmd := exec.Command(payloadPath, os.Args[1:]...)
	cmd.Dir = filepath.Dir(payloadPath)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}

	if err := cmd.Start(); err != nil {
		showError("启动失败", fmt.Sprintf("无法启动 %s：\n%s", appName, err))
	}
}

func hasRequiredRuntime() bool {
	for _, root := range candidateDotnetRoots() {
		if directoryHasRequiredRuntime(filepath.Join(root, "shared", requiredRuntimeName)) {
			return true
		}
	}

	return dotnetListRuntimesHasRequiredRuntime() || registryHasRequiredRuntime()
}

func candidateDotnetRoots() []string {
	var roots []string

	if requiredArchitecture == "x64" {
		roots = append(roots, os.Getenv("DOTNET_ROOT_X64"))
	}

	roots = append(roots, os.Getenv("DOTNET_ROOT"))

	if programW6432 := os.Getenv("ProgramW6432"); programW6432 != "" {
		roots = append(roots, filepath.Join(programW6432, "dotnet"))
	}

	if programFiles := os.Getenv("ProgramFiles"); programFiles != "" {
		roots = append(roots, filepath.Join(programFiles, "dotnet"))
	}

	if requiredArchitecture == "x86" {
		if programFilesX86 := os.Getenv("ProgramFiles(x86)"); programFilesX86 != "" {
			roots = append(roots, filepath.Join(programFilesX86, "dotnet"))
		}
	}

	roots = append(roots, `C:\Program Files\dotnet`)
	return uniqueNonEmptyPaths(roots)
}

func uniqueNonEmptyPaths(paths []string) []string {
	seen := make(map[string]bool, len(paths))
	var result []string

	for _, path := range paths {
		if strings.TrimSpace(path) == "" {
			continue
		}

		clean := filepath.Clean(path)
		key := strings.ToLower(clean)
		if seen[key] {
			continue
		}

		seen[key] = true
		result = append(result, clean)
	}

	return result
}

func directoryHasRequiredRuntime(path string) bool {
	entries, err := os.ReadDir(path)
	if err != nil {
		return false
	}

	for _, entry := range entries {
		if entry.IsDir() && isRequiredMajorVersion(entry.Name()) {
			return true
		}
	}

	return false
}

func dotnetListRuntimesHasRequiredRuntime() bool {
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()

	cmd := exec.CommandContext(ctx, "dotnet", "--list-runtimes")
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}

	output, err := cmd.Output()
	if err != nil {
		return false
	}

	for _, line := range bytes.Split(output, []byte{'\n'}) {
		fields := strings.Fields(string(line))
		if len(fields) >= 2 && fields[0] == requiredRuntimeName && isRequiredMajorVersion(fields[1]) {
			return true
		}
	}

	return false
}

func registryHasRequiredRuntime() bool {
	registryView := "/reg:64"
	if requiredArchitecture == "x86" {
		registryView = "/reg:32"
	}

	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()

	key := `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\` + requiredArchitecture + `\sharedfx\` + requiredRuntimeName
	cmd := exec.CommandContext(ctx, "reg.exe", "query", key, registryView)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}

	output, err := cmd.Output()
	if err != nil {
		return false
	}

	for _, line := range strings.Split(string(output), "\n") {
		version := filepath.Base(strings.TrimSpace(line))
		if isRequiredMajorVersion(version) {
			return true
		}
	}

	return false
}

func isRequiredMajorVersion(version string) bool {
	return versionPattern.MatchString(strings.TrimSpace(version))
}

func resolvePayloadPath() (string, error) {
	exePath, err := os.Executable()
	if err != nil {
		return "", fmt.Errorf("无法定位启动器路径：%w", err)
	}

	payloadPath := filepath.Join(filepath.Dir(exePath), appPayloadExe)
	if _, err := os.Stat(payloadPath); err != nil {
		return "", fmt.Errorf("找不到应用程序文件：\n%s", payloadPath)
	}

	return payloadPath, nil
}

func askToOpenRuntimeDownloadPage() bool {
	message := fmt.Sprintf(
		"%s 需要安装 .NET 8 Desktop Runtime (%s) 才能运行。\n\n请从微软官网下载并安装 .NET 8 Desktop Runtime，然后重新启动 %s。\n\n是否现在打开下载页面？\n%s",
		appName,
		requiredArchitecture,
		appName,
		runtimeDownloadURL,
	)

	return messageBox(
		"需要安装 .NET 8",
		message,
		messageBoxYesNo|messageBoxIconWarning|messageBoxDefaultFirst|messageBoxTopMost,
	) == idYes
}

func showError(title, message string) {
	messageBox(title, message, messageBoxOK|messageBoxIconError|messageBoxTopMost)
}

func messageBox(title, text string, flags uint32) int {
	user32 := syscall.NewLazyDLL("user32.dll")
	messageBoxW := user32.NewProc("MessageBoxW")

	result, _, _ := messageBoxW.Call(
		0,
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(text))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(title))),
		uintptr(flags),
	)

	return int(result)
}

func openURL(url string) {
	shell32 := syscall.NewLazyDLL("shell32.dll")
	shellExecuteW := shell32.NewProc("ShellExecuteW")

	shellExecuteW.Call(
		0,
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("open"))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(url))),
		0,
		0,
		swShowNormal,
	)
}
