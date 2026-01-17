#include "Extensions/Libraries/CSWorldExtensions.h"
#include "UnrealSharpCore.h"
#include "Engine/Engine.h"
#include "GameFramework/Actor.h"

namespace
{
	UWorld* GetFallbackWorld()
	{
		if (!GEngine)
		{
			return nullptr;
		}

		for (const FWorldContext& WorldContext : GEngine->GetWorldContexts())
		{
			UWorld* World = WorldContext.World();
			if (IsValid(World) && World->IsGameWorld())
			{
				return World;
			}
		}

		for (const FWorldContext& WorldContext : GEngine->GetWorldContexts())
		{
			UWorld* World = WorldContext.World();
			if (IsValid(World))
			{
				return World;
			}
		}

		return nullptr;
	}
}

AActor* UCSWorldExtensions::SpawnActor(const UObject* WorldContextObject, const TSubclassOf<AActor>& Class, const FTransform& Transform, const FCSSpawnActorParameters& InSpawnParameters)
{
	return SpawnActor_Internal(WorldContextObject, Class, Transform, InSpawnParameters, false);
}

AActor* UCSWorldExtensions::SpawnActorDeferred(const UObject* WorldContextObject, const TSubclassOf<AActor>& Class, const FTransform& Transform, const FCSSpawnActorParameters& SpawnParameters)
{
	return SpawnActor_Internal(WorldContextObject, Class, Transform, SpawnParameters, true);
}

void UCSWorldExtensions::ExecuteConstruction(AActor* Actor, const FTransform& Transform)
{
	Actor->ExecuteConstruction(Transform, nullptr, nullptr, true);
}

void UCSWorldExtensions::PostActorConstruction(AActor* Actor)
{
	Actor->PostActorConstruction();
	Actor->PostLoad();
}

FURL UCSWorldExtensions::WorldURL(const UObject* WorldContextObject)
{
	if (!IsValid(WorldContextObject))
	{
		UE_LOG(LogUnrealSharp, Error, TEXT("Invalid world context object"));
		return nullptr;
	}

	UWorld* World = GEngine->GetWorldFromContextObject(WorldContextObject, EGetWorldErrorMode::LogAndReturnNull);

	if (!IsValid(World))
	{
		UE_LOG(LogUnrealSharp, Error, TEXT("Failed to get world from context object"));
		return nullptr;
	}

	return World->URL;
}

AActor* UCSWorldExtensions::SpawnActor_Internal(const UObject* WorldContextObject, const TSubclassOf<AActor>& Class, const FTransform& Transform, const FCSSpawnActorParameters& SpawnParameters, bool bDeferConstruction)
{
	if (!IsValid(Class))
	{
		UE_LOG(LogUnrealSharp, Error, TEXT("Invalid class for SpawnActor"));
		return nullptr;
	}

	UWorld* World = nullptr;
	if (IsValid(WorldContextObject))
	{
		World = GEngine->GetWorldFromContextObject(WorldContextObject, EGetWorldErrorMode::ReturnNull);
	}

	if (!IsValid(World))
	{
		World = GetFallbackWorld();
		if (!IsValid(World))
		{
			UE_LOG(LogUnrealSharp, Error, TEXT("Failed to resolve world for SpawnActor"));
			return nullptr;
		}

		UE_LOG(LogUnrealSharp, Warning, TEXT("SpawnActor: Using fallback world context"));
	}

	FActorSpawnParameters SpawnParams;
	SpawnParams.Instigator = SpawnParameters.Instigator;
	SpawnParams.Owner = SpawnParameters.Owner;
	SpawnParams.Template = SpawnParameters.Template;
	SpawnParams.SpawnCollisionHandlingOverride = SpawnParameters.SpawnMethod;
	SpawnParams.bDeferConstruction = bDeferConstruction;
	
	return World->SpawnActor(Class, &Transform, SpawnParams);
}

