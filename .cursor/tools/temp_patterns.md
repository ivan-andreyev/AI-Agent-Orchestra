## Idle Game Architecture Patterns (архитектурные концепции)

### Resource Management Pattern
```csharp
// Idle-specific resource operations:
namespace Game.IdleGame.Commands.Resources
{
    public class AddResourceCommand : GameCommand<AddResourceResponse>
    {
        public string ResourceType { get; set; }
        public decimal Amount { get; set; }
        public string Source { get; set; } // "generator", "manual", "upgrade", etc.
        public bool AllowOverflow { get; set; } = false;
        public decimal? MaxCapacity { get; set; }
    }

    public class AddResourceResponse : GameResponse
    {
        public decimal AmountAdded { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal OverflowAmount { get; set; }
        public bool CapacityReached { get; set; }
    }

    public class AddResourceHandler : IRequestHandler<AddResourceCommand, AddResourceResponse>
    {
        private readonly IResourceRepository _repository;
        private readonly IMediator _mediator;

        public async Task<AddResourceResponse> Handle(AddResourceCommand request, CancellationToken cancellationToken)
        {
            var currentResource = await _repository.GetResourceAsync(request.UserId, request.ResourceType);
            
            // Calculate addition with capacity limits
            var capacity = request.MaxCapacity ?? currentResource.MaxCapacity;
            var availableSpace = capacity - currentResource.Amount;
            var actualAmount = Math.Min(request.Amount, availableSpace);
            var overflow = request.Amount - actualAmount;

            if (!request.AllowOverflow && overflow > 0)
            {
                actualAmount = Math.Min(request.Amount, availableSpace);
                overflow = request.Amount - actualAmount;
            }

            // Update resource
            currentResource.Amount += actualAmount;
            await _repository.SaveResourceAsync(currentResource);

            // Publish event for tracking
            await _mediator.Publish(new ResourceAddedEvent
            {
                UserId = request.UserId,
                ResourceType = request.ResourceType,
                AmountAdded = actualAmount,
                Source = request.Source,
                OverflowAmount = overflow,
                Timestamp = DateTime.UtcNow
            });

            return new AddResourceResponse
            {
                Success = true,
                AmountAdded = actualAmount,
                TotalAmount = currentResource.Amount,
                OverflowAmount = overflow,
                CapacityReached = overflow > 0
            };
        }
    }
}
```

### Upgrade System Pattern
```csharp
// Progressive upgrade mechanics:
namespace Game.IdleGame.Commands.Upgrades
{
    public class PurchaseUpgradeCommand : GameCommand<PurchaseUpgradeResponse>
    {
        public int UpgradeId { get; set; }
        public bool SkipValidation { get; set; } = false;
        public bool ApplyImmediately { get; set; } = true;
    }

    public class PurchaseUpgradeResponse : GameResponse
    {
        public UpgradeInfo PurchasedUpgrade { get; set; }
        public Dictionary<string, decimal> CostPaid { get; set; }
        public List<EffectInfo> EffectsApplied { get; set; }
        public List<UpgradeInfo> NewlyUnlockedUpgrades { get; set; }
    }

    public class PurchaseUpgradeHandler : IRequestHandler<PurchaseUpgradeCommand, PurchaseUpgradeResponse>
    {
        private readonly IUpgradeRepository _upgradeRepository;
        private readonly IResourceRepository _resourceRepository;
        private readonly IMediator _mediator;

        public async Task<PurchaseUpgradeResponse> Handle(PurchaseUpgradeCommand request, CancellationToken cancellationToken)
        {
            // Get upgrade definition
            var upgrade = await _upgradeRepository.GetUpgradeAsync(request.UpgradeId);
            if (upgrade == null)
                return new PurchaseUpgradeResponse { Success = false, ErrorMessage = "Upgrade not found" };

            // Check prerequisites
            if (!request.SkipValidation)
            {
                var prerequisiteCheck = await ValidatePrerequisitesAsync(request.UserId, upgrade);
                if (!prerequisiteCheck.IsValid)
                    return new PurchaseUpgradeResponse { Success = false, ErrorMessage = prerequisiteCheck.ErrorMessage };
            }

            // Calculate current cost (may scale with level)
            var currentCost = CalculateUpgradeCost(upgrade);
            
            // Check affordability
            var affordabilityCheck = await CanAffordUpgradeAsync(request.UserId, currentCost);
            if (!affordabilityCheck.CanAfford)
                return new PurchaseUpgradeResponse { Success = false, ErrorMessage = "Insufficient resources" };

            // Deduct cost
            foreach (var cost in currentCost)
            {
                await _mediator.Send(new SpendResourceCommand
                {
                    UserId = request.UserId,
                    ResourceType = cost.Key,
                    Amount = cost.Value,
                    Source = $"upgrade_{upgrade.Id}"
                });
            }

            // Apply upgrade
            var playerUpgrade = await _upgradeRepository.GetPlayerUpgradeAsync(request.UserId, request.UpgradeId);
            if (playerUpgrade == null)
            {
                playerUpgrade = new PlayerUpgrade
                {
                    UserId = request.UserId,
                    UpgradeId = request.UpgradeId,
                    Level = 0,
                    PurchasedAt = DateTime.UtcNow
                };
            }

            playerUpgrade.Level++;
            playerUpgrade.LastUpgradedAt = DateTime.UtcNow;
            await _upgradeRepository.SavePlayerUpgradeAsync(playerUpgrade);

            // Apply effects if requested
            var effectsApplied = new List<EffectInfo>();
            if (request.ApplyImmediately)
            {
                effectsApplied = await ApplyUpgradeEffectsAsync(request.UserId, upgrade, playerUpgrade.Level);
            }

            // Check for newly unlocked upgrades
            var newlyUnlocked = await CheckUnlockedUpgradesAsync(request.UserId);

            // Publish event
            await _mediator.Publish(new UpgradePurchasedEvent
            {
                UserId = request.UserId,
                UpgradeId = request.UpgradeId,
                NewLevel = playerUpgrade.Level,
                CostPaid = currentCost,
                Timestamp = DateTime.UtcNow
            });

            return new PurchaseUpgradeResponse
            {
                Success = true,
                PurchasedUpgrade = new UpgradeInfo
                {
                    Id = upgrade.Id,
                    Name = upgrade.Name,
                    Level = playerUpgrade.Level,
                    NextCost = CalculateUpgradeCost(upgrade, playerUpgrade.Level + 1)
                },
                CostPaid = currentCost,
                EffectsApplied = effectsApplied,
                NewlyUnlockedUpgrades = newlyUnlocked
            };
        }

        private Dictionary<string, decimal> CalculateUpgradeCost(UpgradeDefinition upgrade, int targetLevel = -1)
        {
            var level = targetLevel == -1 ? upgrade.CurrentLevel + 1 : targetLevel;
            var cost = new Dictionary<string, decimal>();
            
            foreach (var baseCost in upgrade.BaseCost)
            {
                var scaledCost = baseCost.Value * Math.Pow(upgrade.CostScaling, level - 1);
                cost[baseCost.Key] = Math.Ceiling(scaledCost);
            }
            
            return cost;
        }
    }
}
```

### Generator Automation Pattern
```csharp
// Automated resource generation:
namespace Game.IdleGame.Commands.Generators
{
    public class ProcessGenerationCommand : GameCommand<ProcessGenerationResponse>
    {
        public TimeSpan ElapsedTime { get; set; }
        public List<int> GeneratorIds { get; set; } = new(); // Empty = all active generators
        public bool ApplyUpgradeEffects { get; set; } = true;
        public bool DistributeEvenly { get; set; } = false; // For load balancing
    }

    public class ProcessGenerationResponse : GameResponse
    {
        public Dictionary<string, decimal> ResourcesGenerated { get; set; } = new();
        public List<GeneratorResult> GeneratorResults { get; set; } = new();
        public TimeSpan ActualProcessTime { get; set; }
        public decimal TotalEfficiency { get; set; }
    }

    public class GeneratorResult
    {
        public int GeneratorId { get; set; }
        public string GeneratorName { get; set; }
        public Dictionary<string, decimal> ResourcesProduced { get; set; }
        public decimal BaseRate { get; set; }
        public decimal ActualRate { get; set; }
        public decimal EfficiencyMultiplier { get; set; }
        public List<string> ActiveBonuses { get; set; } = new();
    }

    public class ProcessGenerationHandler : IRequestHandler<ProcessGenerationCommand, ProcessGenerationResponse>
    {
        private readonly IGeneratorRepository _generatorRepository;
        private readonly IResourceRepository _resourceRepository;
        private readonly IUpgradeRepository _upgradeRepository;
        private readonly IMediator _mediator;

        public async Task<ProcessGenerationResponse> Handle(ProcessGenerationCommand request, CancellationToken cancellationToken)
        {
            var response = new ProcessGenerationResponse
            {
                Success = true,
                ActualProcessTime = request.ElapsedTime
            };

            // Get active generators
            var generators = await GetActiveGeneratorsAsync(request.UserId, request.GeneratorIds);
            if (!generators.Any())
            {
                return response; // No generators to process
            }

            // Calculate upgrade effects if requested
            var upgradeEffects = request.ApplyUpgradeEffects 
                ? await CalculateUpgradeEffectsAsync(request.UserId)
                : new Dictionary<string, decimal>();

            var totalResources = new Dictionary<string, decimal>();

            foreach (var generator in generators)
            {
                var generatorResult = await ProcessSingleGeneratorAsync(
                    generator, 
                    request.ElapsedTime, 
                    upgradeEffects);

                response.GeneratorResults.Add(generatorResult);

                // Accumulate resources
                foreach (var resource in generatorResult.ResourcesProduced)
                {
                    if (!totalResources.ContainsKey(resource.Key))
                        totalResources[resource.Key] = 0;
                    totalResources[resource.Key] += resource.Value;
                }
            }

            // Apply resources to player inventory
            foreach (var resource in totalResources)
            {
                var addResult = await _mediator.Send(new AddResourceCommand
                {
                    UserId = request.UserId,
                    ResourceType = resource.Key,
                    Amount = resource.Value,
                    Source = "generator",
                    AllowOverflow = false
                });

                response.ResourcesGenerated[resource.Key] = addResult.AmountAdded;
            }

            // Calculate overall efficiency
            response.TotalEfficiency = CalculateOverallEfficiency(response.GeneratorResults);

            // Publish generation event
            await _mediator.Publish(new ResourcesGeneratedEvent
            {
                UserId = request.UserId,
                ResourcesGenerated = response.ResourcesGenerated,
                GeneratorsUsed = response.GeneratorResults.Count,
                ElapsedTime = request.ElapsedTime,
                Timestamp = DateTime.UtcNow
            });

            return response;
        }

        private async Task<GeneratorResult> ProcessSingleGeneratorAsync(
            GeneratorDefinition generator, 
            TimeSpan elapsedTime, 
            Dictionary<string, decimal> upgradeEffects)
        {
            var result = new GeneratorResult
            {
                GeneratorId = generator.Id,
                GeneratorName = generator.Name,
                BaseRate = generator.BaseGenerationRate,
                ResourcesProduced = new Dictionary<string, decimal>()
            };

            // Calculate efficiency multiplier
            var efficiencyMultiplier = 1.0m;
            
            // Apply upgrade effects
            foreach (var effect in upgradeEffects.Where(e => e.Key.StartsWith($"generator_{generator.Id}_")))
            {
                efficiencyMultiplier *= effect.Value;
                result.ActiveBonuses.Add(effect.Key);
            }

            // Apply global effects
            if (upgradeEffects.ContainsKey("global_generation_multiplier"))
            {
                efficiencyMultiplier *= upgradeEffects["global_generation_multiplier"];
                result.ActiveBonuses.Add("global_generation_multiplier");
            }

            result.EfficiencyMultiplier = efficiencyMultiplier;
            result.ActualRate = result.BaseRate * efficiencyMultiplier;

            // Calculate production for each resource type
            foreach (var resourceProduction in generator.ResourceProduction)
            {
                var baseAmount = resourceProduction.Value * result.ActualRate * (decimal)elapsedTime.TotalSeconds;
                var finalAmount = Math.Floor(baseAmount * 100) / 100; // Round to 2 decimal places
                
                if (finalAmount > 0)
                {
                    result.ResourcesProduced[resourceProduction.Key] = finalAmount;
                }
            }

            return result;
        }

        private decimal CalculateOverallEfficiency(List<GeneratorResult> results)
        {
            if (!results.Any()) return 0;
            
            return results.Average(r => r.EfficiencyMultiplier);
        }
    }
}
```

### Progress Tracking Pattern
```csharp
// Progress и achievement tracking:
namespace Game.IdleGame.Commands.Progress
{
    public class UpdateProgressCommand : GameCommand<UpdateProgressResponse>
    {
        public string ProgressType { get; set; } // "resource", "upgrade", "time_played", etc.
        public string ProgressKey { get; set; } // specific identifier
        public decimal Amount { get; set; }
        public bool CheckMilestones { get; set; } = true;
        public bool CheckAchievements { get; set; } = true;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class UpdateProgressResponse : GameResponse
    {
        public ProgressInfo UpdatedProgress { get; set; }
        public List<MilestoneInfo> MilestonesReached { get; set; } = new();
        public List<AchievementInfo> AchievementsUnlocked { get; set; } = new();
        public bool LeveledUp { get; set; }
        public int? NewLevel { get; set; }
        public List<RewardInfo> RewardsEarned { get; set; } = new();
    }

    public class ProgressInfo
    {
        public string Type { get; set; }
        public string Key { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal NextMilestone { get; set; }
        public decimal PercentToNextMilestone { get; set; }
    }

    public class UpdateProgressHandler : IRequestHandler<UpdateProgressCommand, UpdateProgressResponse>
    {
        private readonly IProgressRepository _progressRepository;
        private readonly IAchievementRepository _achievementRepository;
        private readonly IMediator _mediator;

        public async Task<UpdateProgressResponse> Handle(UpdateProgressCommand request, CancellationToken cancellationToken)
        {
            var response = new UpdateProgressResponse { Success = true };

            // Get current progress
            var progress = await _progressRepository.GetProgressAsync(
                request.UserId, 
                request.ProgressType, 
                request.ProgressKey);

            if (progress == null)
            {
                progress = new PlayerProgress
                {
                    UserId = request.UserId,
                    Type = request.ProgressType,
                    Key = request.ProgressKey,
                    Value = 0,
                    LastUpdated = DateTime.UtcNow
                };
            }

            var previousValue = progress.Value;
            progress.Value += request.Amount;
            progress.LastUpdated = DateTime.UtcNow;

            // Save updated progress
            await _progressRepository.SaveProgressAsync(progress);

            response.UpdatedProgress = new ProgressInfo
            {
                Type = request.ProgressType,
                Key = request.ProgressKey,
                CurrentValue = progress.Value,
                PreviousValue = previousValue
            };

            // Check for milestones
            if (request.CheckMilestones)
            {
                var milestones = await CheckMilestonesAsync(request.UserId, progress);
                response.MilestonesReached.AddRange(milestones);

                // Update next milestone info
                var nextMilestone = await GetNextMilestoneAsync(request.UserId, progress);
                if (nextMilestone.HasValue)
                {
                    response.UpdatedProgress.NextMilestone = nextMilestone.Value;
                    response.UpdatedProgress.PercentToNextMilestone = 
                        (progress.Value / nextMilestone.Value) * 100;
                }
            }

            // Check for achievements
            if (request.CheckAchievements)
            {
                var achievements = await CheckAchievementsAsync(request.UserId, progress, request.Context);
                response.AchievementsUnlocked.AddRange(achievements);
            }

            // Check for level up (if applicable)
            var levelInfo = await CheckLevelUpAsync(request.UserId, progress);
            if (levelInfo.HasLeveledUp)
            {
                response.LeveledUp = true;
                response.NewLevel = levelInfo.NewLevel;
                response.RewardsEarned.AddRange(levelInfo.Rewards);
            }

            // Publish progress event
            await _mediator.Publish(new ProgressUpdatedEvent
            {
                UserId = request.UserId,
                ProgressType = request.ProgressType,
                ProgressKey = request.ProgressKey,
                OldValue = previousValue,
                NewValue = progress.Value,
                AmountAdded = request.Amount,
                MilestonesReached = response.MilestonesReached.Count,
                AchievementsUnlocked = response.AchievementsUnlocked.Count,
                LeveledUp = response.LeveledUp,
                Timestamp = DateTime.UtcNow
            });

            return response;
        }

        private async Task<List<MilestoneInfo>> CheckMilestonesAsync(string userId, PlayerProgress progress)
        {
            var milestones = new List<MilestoneInfo>();
            
            // Get milestone definitions for this progress type
            var milestoneDefinitions = await _progressRepository.GetMilestoneDefinitionsAsync(
                progress.Type, 
                progress.Key);

            foreach (var milestone in milestoneDefinitions)
            {
                // Check if milestone was just reached
                if (progress.Value >= milestone.Threshold && 
                    !await _progressRepository.IsMilestoneReachedAsync(userId, milestone.Id))
                {
                    // Mark milestone as reached
                    await _progressRepository.MarkMilestoneReachedAsync(userId, milestone.Id);
                    
                    milestones.Add(new MilestoneInfo
                    {
                        Id = milestone.Id,
                        Name = milestone.Name,
                        Description = milestone.Description,
                        Threshold = milestone.Threshold,
                        Rewards = milestone.Rewards
                    });

                    // Award milestone rewards
                    foreach (var reward in milestone.Rewards)
                    {
                        await _mediator.Send(new AwardRewardCommand
                        {
                            UserId = userId,
                            RewardType = reward.Type,
                            RewardValue = reward.Value,
                            Source = $"milestone_{milestone.Id}"
                        });
                    }
                }
            }

            return milestones;
        }

        private async Task<List<AchievementInfo>> CheckAchievementsAsync(
            string userId, 
            PlayerProgress progress, 
            Dictionary<string, object> context)
        {
            var achievements = new List<AchievementInfo>();
            
            // Get achievement definitions that might be triggered by this progress
            var achievementDefinitions = await _achievementRepository.GetTriggeredAchievementsAsync(
                progress.Type, 
                progress.Key);

            foreach (var achievement in achievementDefinitions)
            {
                // Check if achievement conditions are met
                var conditionsMet = await EvaluateAchievementConditionsAsync(
                    userId, 
                    achievement, 
                    progress, 
                    context);

                if (conditionsMet && 
                    !await _achievementRepository.IsAchievementUnlockedAsync(userId, achievement.Id))
                {
                    // Unlock achievement
                    await _achievementRepository.UnlockAchievementAsync(userId, achievement.Id);
                    
                    achievements.Add(new AchievementInfo
                    {
                        Id = achievement.Id,
                        Name = achievement.Name,
                        Description = achievement.Description,
                        Points = achievement.Points,
                        Rarity = achievement.Rarity,
                        Rewards = achievement.Rewards
                    });

                    // Award achievement rewards
                    foreach (var reward in achievement.Rewards)
                    {
                        await _mediator.Send(new AwardRewardCommand
                        {
                            UserId = userId,
                            RewardType = reward.Type,
                            RewardValue = reward.Value,
                            Source = $"achievement_{achievement.Id}"
                        });
                    }
                }
            }

            return achievements;
        }
    }
}
```
