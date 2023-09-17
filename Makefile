SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""

clean:
	dotnet clean -c $(CONFIGURATION) && rm -rf $(ARTIFACT_PATH)

build: clean
	dotnet build -c $(CONFIGURATION)

test: clean
	dotnet test -c $(CONFIGURATION)

pack: test
	dotnet pack -c $(CONFIGURATION)

push: pack
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

.DEFAULT_GOAL := test