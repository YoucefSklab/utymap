﻿using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Plugins;
using Assets.Scripts.Scenes.Customization.Plugins;
using UnityEngine;
using UtyMap.Unity;
using UtyMap.Unity.Data;
using UtyMap.Unity.Infrastructure.Diagnostic;
using UtyMap.Unity.Infrastructure.Primitives;
using UtyMap.Unity.Utils;
using UtyRx;
using Component = UtyDepend.Component;

namespace Assets.Scripts.Scenes.Customization
{
    /// <summary> Demonstrates different aspects of customization. </summary>
    /// <remarks>
    ///     This scene shows the following use-cases:
    ///     1. How to use custom logic for building GameObject from existing map data
    ///     2. How to use custom prefab for selected by id element
    ///     3. How to add a new element (e.g. tree) into map data.
    /// </remarks>
    public class CustomizationBehaviour : MonoBehaviour
    {
        /// <summary> Path to map data on disk. </summary>
        private const string MapDataPath = @"../../../../core/test/test_assets/osm/berlin.osm.xml";

        /// <summary> Start coordinate: Unity's world zero point. </summary>
        private readonly GeoCoordinate _coordinate = new GeoCoordinate(52.5317429, 13.3871987);

        private CompositionRoot _compositionRoot;
        private IMapDataStore _mapDataStore;

        void Start()
        {
            // init utymap library
            _compositionRoot = InitTask.Run((container, config) =>
            {
                container
                    .Register(Component.For<Stylesheet>().Use<Stylesheet>(@"mapcss/customization/customization.mapcss"))
                    .Register(Component.For<MaterialProvider>().Use<MaterialProvider>())
                    .Register(Component.For<GameObjectBuilder>().Use<GameObjectBuilder>())
                    // NOTE for use case 1
                    .Register(Component.For<IElementBuilder>().Use<PlaceElementBuilder>().Named("place"))
                    // NOTE for use case 2
                    .Register(Component.For<IElementBuilder>().Use<ImportElementBuilder>().Named("import"));
            });

            // store map data store reference to member variable
            _mapDataStore = _compositionRoot.GetService<IMapDataStore>();

            // disable mesh caching to force import data into memory for every run
            _compositionRoot.GetService<IMapDataLibrary>().DisableCache();

            // import data into memory
            _mapDataStore.Add(
                    // define where geoindex is created (in memory, not persistent)
                    MapDataStorageType.InMemory,
                    // path to map data
                    MapDataPath,
                    // stylesheet is used to import only used data and skip unused
                    _compositionRoot.GetService<Stylesheet>(),
                    // level of detail (zoom) for which map data should be imported
                    new Range<int>(16, 16))
                // start import and listen for events.
                .Subscribe(
                    // NOTE progress callback is ignored
                    (progress) => { },
                    // exception is reported
                    (exception) => _compositionRoot.GetService<ITrace>().Error("import", exception, "Cannot import map data"),
                    // once completed, load the corresponding tile
                    OnDataImported);
        }

        private void OnDataImported()
        {
            AddTree();
            LoadTile();
        }

        /// <summary> Adds tree to in-memory tree. </summary>
        private void AddTree()
        {
            // NOTE use case 3
            // create an element which represents a tree
            Element tree = new Element(
                // id of the object
                0,
                // geo coordinate: make a tree on the road
                new [] { new GeoCoordinate(52.53138, 13.38827) },
                // height in meters under sea
                new [] { .0 },
                // map data tags
                new Dictionary<string, string> { {"natural", "tree"} },
                // styles: ignored by editor
                new Dictionary<string, string>());

            _compositionRoot
                .GetService<IMapDataEditor>()
                .Add(
                    // type of storage
                    MapDataStorageType.InMemory,
                    // element to store
                    tree,
                    // LOD range where element is stored
                    new Range<int>(16, 16));
        }

        /// <summary> Start loading of the tile. </summary>
        private void LoadTile()
        {
            _mapDataStore.OnNext(new Tile(
                // create quadkey using coordinate and LOD
                GeoUtils.CreateQuadKey(_coordinate, 16),
                // provide stylesheet
                _compositionRoot.GetService<Stylesheet>(),
                // use cartesian projection as we want to build flat world
                new CartesianProjection(_coordinate),
                // use flat elevation (all vertices have zero meters elevation)
                ElevationDataType.Flat,
                // parent for built game objects
                gameObject));
        }
    }
}
