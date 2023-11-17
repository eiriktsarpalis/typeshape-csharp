SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true
CODECOV_ARGS ?= --collect:"XPlat Code Coverage" --results-directory $(ARTIFACT_PATH)

clean:
	dotnet clean -c $(CONFIGURATION) && rm -rf $(ARTIFACT_PATH)

build: clean
	dotnet build -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

test: clean
	dotnet test -c $(CONFIGURATION) $(ADDITIONAL_ARGS) $(CODECOV_ARGS)

pack: test
	dotnet pack -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

push: pack
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

.DEFAULT_GOAL := test