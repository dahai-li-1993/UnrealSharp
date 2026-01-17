#include "Extensions/Libraries/CSUserWidgetExtensions.h"
#include "UnrealSharpCore.h"
#include "GameFramework/PlayerState.h"
#include "Blueprint/UserWidget.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Blueprint/WidgetTree.h"
#include "Engine/Engine.h"

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

APlayerController* UCSUserWidgetExtensions::GetOwningPlayerController(UUserWidget* UserWidget)
{
	if (!IsValid(UserWidget))
	{
		return nullptr;
	}

	return UserWidget->GetOwningPlayer();
}

void UCSUserWidgetExtensions::SetOwningPlayerController(UUserWidget* UserWidget, APlayerController* PlayerController)
{
	if (!IsValid(UserWidget))
	{
		return;
	}

	UserWidget->SetOwningPlayer(PlayerController);
}

APlayerState* UCSUserWidgetExtensions::GetOwningPlayerState(UUserWidget* UserWidget)
{
	if (!IsValid(UserWidget))
	{
		return nullptr;
	}
	
	return UserWidget->GetOwningPlayerState<APlayerState>();
}

ULocalPlayer* UCSUserWidgetExtensions::GetOwningLocalPlayer(UUserWidget* UserWidget)
{
	if (!IsValid(UserWidget))
	{
		return nullptr;
	}

	return UserWidget->GetOwningLocalPlayer();
}

UUserWidget* UCSUserWidgetExtensions::CreateWidget(UObject* WorldContextObject, const TSubclassOf<UUserWidget>& UserWidgetClass, APlayerController* OwningController)
{
	UObject* ResolvedContext = WorldContextObject;
	if (!IsValid(ResolvedContext) || GEngine->GetWorldFromContextObject(ResolvedContext, EGetWorldErrorMode::ReturnNull) == nullptr)
	{
		if (IsValid(OwningController))
		{
			ResolvedContext = OwningController;
		}
		else
		{
			ResolvedContext = GetFallbackWorld();
		}
	}

	if (!IsValid(ResolvedContext))
	{
		UE_LOG(LogUnrealSharp, Error, TEXT("CreateWidget: Failed to resolve world context"));
		return nullptr;
	}

	UUserWidget* UserWidget = UWidgetBlueprintLibrary::Create(ResolvedContext, UserWidgetClass, OwningController);
	return UserWidget;
}

TArray<UWidget*> UCSUserWidgetExtensions::GetAllWidgets(UUserWidget* UserWidget)
{
	TArray<UWidget*> Widgets;
	if (!IsValid(UserWidget) || !IsValid(UserWidget->WidgetTree))
	{
		return Widgets;
	}

	UserWidget->WidgetTree->GetAllWidgets(Widgets);
	return Widgets;
}
