#if FUSION
using Fusion;
using System;

public class ModPrefabSource : INetworkPrefabSource
{
    private readonly NetworkObject _prefabReference;
    private readonly NetworkObjectGuid _assetGuid;
    private bool _isAcquired;

    public ModPrefabSource(NetworkObject prefab)
    {
        _prefabReference = prefab ?? throw new ArgumentNullException(nameof(prefab));
        _assetGuid = NetworkObjectGuid.Parse(Guid.NewGuid().ToString());
    }

    public NetworkObjectGuid AssetGuid => _assetGuid;

    public bool IsCompleted => _isAcquired;

    public string Description => $"Mod Prefab: {_prefabReference.name}";

    public void Acquire(bool synchronous)
    {
        _isAcquired = true;
    }

    public NetworkObject WaitForResult()
    {
        if (!_isAcquired)
        {
            throw new InvalidOperationException("Cannot get result before Acquire has been called.");
        }
        return _prefabReference;
    }

    public void Release()
    {
        _isAcquired = false;
    }
}

#endif