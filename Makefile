PROJECT := SmsRelay/SmsRelay.csproj
FRAMEWORK := net10.0-android
CONFIGURATION ?= Debug
.DEFAULT_GOAL := help
PUBLISH_DIR := publish

# Override either value when your SDK/JDK is installed elsewhere.
export ANDROID_SDK_ROOT ?= $(HOME)/Library/Android/sdk
export JAVA_HOME ?= /opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home

DOTNET_ANDROID_PROPERTIES := -p:AndroidSdkDirectory=$(ANDROID_SDK_ROOT) -p:JavaSdkDirectory=$(JAVA_HOME)

.PHONY: help restore build apk clean

help:
	@echo "Targets:"
	@echo "  make restore  Restore NuGet packages"
	@echo "  make build    Build the Android app (default: Debug)"
	@echo "  make apk      Create a signed APK and copy it to ./$(PUBLISH_DIR)/"
	@echo "  make clean    Remove build output"
	@echo ""
	@echo "Parameters:"
	@echo "  CONFIGURATION=Debug|Release       Build configuration (default: Debug)"
	@echo "  ANDROID_SDK_ROOT=/path/to/sdk     Android SDK location"
	@echo "  JAVA_HOME=/path/to/jdk            JDK 21 location"
	@echo ""
	@echo "Examples:"
	@echo "  make build CONFIGURATION=Release"
	@echo "  make apk CONFIGURATION=Release"
	@echo "  make build ANDROID_SDK_ROOT=/path/to/sdk JAVA_HOME=/path/to/jdk"

restore:
	dotnet workload restore $(PROJECT)
	dotnet restore $(PROJECT) $(DOTNET_ANDROID_PROPERTIES)

build:
	dotnet build $(PROJECT) -f $(FRAMEWORK) -c $(CONFIGURATION) $(DOTNET_ANDROID_PROPERTIES)

apk:
	dotnet publish $(PROJECT) -f $(FRAMEWORK) -c $(CONFIGURATION) $(DOTNET_ANDROID_PROPERTIES) -p:AndroidPackageFormats=apk
	mkdir -p $(PUBLISH_DIR)
	rm -f $(PUBLISH_DIR)/*.apk
	find SmsRelay/bin/$(CONFIGURATION)/$(FRAMEWORK)/publish -name '*-Signed.apk' -type f -exec cp -f {} $(PUBLISH_DIR)/ \;
	@echo "Signed APK copied to ./$(PUBLISH_DIR)/"

clean:
	dotnet clean $(PROJECT) -f $(FRAMEWORK) -c $(CONFIGURATION) $(DOTNET_ANDROID_PROPERTIES)
