"""Command class library for all known process map commands.

Classes whose payload structure was verified against real payloads from a
production map ("Starlight - UG Survey.ddf") are marked VERIFIED. The rest
are stubs generated from the official help docs (see docs/): their internal
Tag names are guessed by CamelCasing the doc title and marked UNVERIFIED —
confirm against a real map before relying on them.

Payload delimiter levels (outermost first):
    **||**          tag segments (handled in deswik_pm.tag)
    ~~~^^^~~~       positional args / rows          (ARG_SEP)
    ~~~~^^^^~~~~    key:value pairs within a row    (PAIR_SEP)
    ***|||***       sub-sections in some payloads
    |||             compact positional args (menu Command, ...)
    ##||##          list items inside a value
"""
from __future__ import annotations

from ..tag import ARG_SEP, CommandEntry
from .base import DataSetCommand, DelimitedCommand, GenericCommand
from .message_box import MessageBox

PAIR_SEP = "~~~~^^^^~~~~"
BAR_SEP = "|||"


# --------------------------------------------------------------------------
# VERIFIED delimited commands
# --------------------------------------------------------------------------

class MenuCommand(GenericCommand):
    """Run a Deswik.CAD menu command by CID. VERIFIED.

    Payload: ``CID_xxx|||<flag1>|||<flag2>`` (flags observed True/False;
    meaning not yet confirmed — likely 'wait for completion' style options).
    """

    name = "Command"

    def __init__(self, command_id: str = "", flag1: bool = False, flag2: bool = True):
        self.command_id, self.flag1, self.flag2 = command_id, flag1, flag2

    @property
    def payload(self) -> str:
        return BAR_SEP.join([self.command_id, str(self.flag1), str(self.flag2)])

    @payload.setter
    def payload(self, raw: str) -> None:
        parts = raw.split(BAR_SEP)
        self.command_id = parts[0] if parts else ""
        self.flag1 = len(parts) > 1 and parts[1] == "True"
        self.flag2 = len(parts) > 2 and parts[2] == "True"

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj


class OpenProcessMap(GenericCommand):
    """Load another process map, replacing the current one. VERIFIED.

    Payload: the target process map name (no delimiters).
    """

    name = "Workflow"

    def __init__(self, map_name: str = ""):
        self.map_name = map_name

    @property
    def payload(self) -> str:
        return self.map_name

    @payload.setter
    def payload(self, raw: str) -> None:
        self.map_name = raw

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        return cls(entry.payload)


class EmbeddedMacro(GenericCommand):
    """VB macro source embedded on the node. VERIFIED.

    Payload: raw macro source text (with '#Reference # header lines).
    """

    name = "EmbeddedMacro"

    def __init__(self, source: str = ""):
        self.source = source

    @property
    def payload(self) -> str:
        return self.source

    @payload.setter
    def payload(self, raw: str) -> None:
        self.source = raw

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        return cls(entry.payload)


class ExecuteFile(DelimitedCommand):
    """Open a file or run an executable. VERIFIED.

    Payload: path ~^~ args ~^~ flag1 ~^~ flag2 ~^~ timeout_seconds
    """

    name = "ExecuteFile"
    fields = ["path", "args", "flag1", "flag2", "timeout_seconds"]


class DisplayProcessMapLayer(GenericCommand):
    """Show only nodes on a selected process map layer. VERIFIED.

    Payload: ``flag|||layer_name||||||flag2|||`` (empty fields preserved).
    """

    name = "DisplayProcessMapLayer"

    def __init__(self, layer_name: str = "", parts: list[str] | None = None):
        self.parts = parts or ["False", layer_name, "", "False", ""]

    @property
    def layer_name(self) -> str:
        return self.parts[1] if len(self.parts) > 1 else ""

    @property
    def payload(self) -> str:
        return BAR_SEP.join(self.parts)

    @payload.setter
    def payload(self, raw: str) -> None:
        self.parts = raw.split(BAR_SEP)

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj


class SetNodeStatus(GenericCommand):
    """Change the status of other nodes when this node runs. VERIFIED.

    Payload: rows joined by ARG_SEP; each row is
    ``NodeName:<name>~~~~^^^^~~~~CompletionStatus:<status>``.
    Statuses: Incomplete, CompleteWithErrors, CompleteWithoutErrors.
    NodeName <All> targets every node.
    """

    name = "SetNodeStatus"

    def __init__(self, rows: list[tuple[str, str]] | None = None):
        self.rows = rows or []  # [(node_name, status)]

    @property
    def payload(self) -> str:
        return ARG_SEP.join(
            f"NodeName:{n}{PAIR_SEP}CompletionStatus:{s}" for n, s in self.rows
        )

    @payload.setter
    def payload(self, raw: str) -> None:
        self.rows = []
        for row in raw.split(ARG_SEP):
            if not row:
                continue
            pairs = dict(
                p.split(":", 1) for p in row.split(PAIR_SEP) if ":" in p
            )
            self.rows.append(
                (pairs.get("NodeName", ""), pairs.get("CompletionStatus", ""))
            )

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj


class DocumentSettings(GenericCommand):
    """Apply imported file settings to the active file. VERIFIED.

    Payload: ``Key:Value`` flags joined by ARG_SEP (Filters, Legends,
    DateRanges, ParameterTables, Curves, GlobalConstants, Toolbars,
    ImportISSettings, ImportAllISSettings, Shortcuts) plus a
    ``SettingsXML:<gzip+base64 blob>`` carrying the actual settings.
    """

    name = "DocumentSettings"

    def __init__(self, options: dict[str, str] | None = None):
        self.options = dict(options or {})

    @property
    def payload(self) -> str:
        return ARG_SEP.join(f"{k}:{v}" for k, v in self.options.items())

    @payload.setter
    def payload(self, raw: str) -> None:
        self.options = {}
        for part in raw.split(ARG_SEP):
            key, _, value = part.partition(":")
            self.options[key] = value

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj


class NodeStatus(GenericCommand):
    """Status indicator node linked to another node's outcome. VERIFIED format,
    structured editing not yet implemented (contains per-status ButtonType/
    Color/ImageXML with base64 PNG payloads). Payload kept verbatim."""

    name = "NodeStatus"


class ApplyLayout(GenericCommand):
    """Apply a saved grid/Gantt layout by name. VERIFIED (payload observed in
    a real Deswik.Sched process map, *.xdf).

    Payload: the layout name only, no delimiters (e.g.
    "Troubleshooting - Critical path").
    """

    name = "ApplyLayout"


class JsonCommandList(GenericCommand):
    """A node's sub-commands encoded as JSON instead of the ``**||**`` Tag
    grammar. Seen on Deswik.Sched process map nodes (*.xdf), not Deswik.CAD
    (*.ddf) ones. VERIFIED format, structured editing not yet implemented
    (payload is
    ``{"Commands": [{"CommandType", "Use", "Description", "Details": {...}}, ...]}``
    with each item mirroring a normal command). Payload kept verbatim."""

    name = "JsonCommandList"


# --------------------------------------------------------------------------
# VERIFIED DataSet (settings grid) commands
# --------------------------------------------------------------------------

def _dataset(name_: str, doc: str):
    return type(name_, (DataSetCommand,), {"name": name_, "__doc__": doc + " VERIFIED (DataSet payload)."})


class CreateLayers(DataSetCommand):
    """Create multiple layers with individual properties. VERIFIED
    (schema captured from a hand-made sample, Suite 2024.2, docs/samples/CreateLayers.txt).

    Use add_layer(name, ...) to append layers, then to_entry().
    """

    name = "CreateLayers"

    # per-layer defaults exactly as Deswik writes them for a plain layer
    LAYER_DEFAULTS = [
        ("Name", ""),
        ("HonorAttributeRules", "False"),
        ("AttributesListAttributeCount", "0"),
        ("LayerProps_LayerName", ""),
        ("LayerProps_LayerPath", ""),
        ("LayerProps_ParentLayerName", ""),
        ("LayerProps_IsVisible", "True"),
        ("LayerProps_IsLocked", "False"),
        ("LayerProps_IsLoaded", "False"),
        ("LayerProps_IsImportLoaded", "False"),
        ("LayerProps_IsDynamic", "False"),
        ("LayerProps_IsRelativeDynamic", "False"),
        ("LayerProps_IsRelativeDataSource", "False"),
        ("LayerProps_IsChildLayer", "False"),
        ("LayerProps_IsChildDataLayer", "False"),
        ("LayerProps_IsLoadedEnabled", "False"),
        ("LayerProps_IsImported", "False"),
        ("LayerProps_IsReferenceOnly", "False"),
        ("LayerProps_LineWeight", "-3"),
        ("LayerProps_Description", ""),
        ("LayerProps_DynamicFile", ""),
        ("LayerProps_DatasourceFile", ""),
        ("LayerProps_DatasourceFileType", ""),
        ("LayerProps_DatasourceDataType", ""),
        ("LayerProps_DatasourceLayer", ""),
        ("LayerProps_DatasourceFilterString", ""),
        ("LayerProps_DatasourceLoadAllLayers", "False"),
        ("LayerProps_ApplyChangesToChildLayers", "True"),
        ("LayerProps_Icon", ""),
        ("LayerProps_PenColor_IsByBlock", "False"),
        ("LayerProps_PenColor_IsByLayer", "False"),
        ("LayerProps_PenColor_IsByColorIndex", "True"),
        ("LayerProps_PenColor_ColorIndex", "6"),
        ("LayerProps_PenColor_SystemColor", "-1"),
        ("LayerProps_PenColor_Transparency", "255"),
        ("LayerProps_PenColor_KeepColorInLayouts", "False"),
        ("LayerProps_LineType", "SOLID"),
        ("LayerProps_NodeBackColor", "16777215"),
    ]

    def __init__(self):
        super().__init__()
        self.layers: list[dict[str, str]] = []

    def add_layer(self, name: str, **overrides: str) -> None:
        layer = dict(self.LAYER_DEFAULTS)
        layer["Name"] = name
        for key, value in overrides.items():
            layer[key] = str(value)
        self.layers.append(layer)
        self._rebuild()

    def _rebuild(self) -> None:
        s = {
            "_EditNodeCreateLayers_SplitPosition1": "252",
            "_EditNodeCreateLayers_SplitPosition2": "392",
            "_EditNodeCreateLayers_OverwriteIconsInDocument": "True",
            "_EditNodeCreateLayers_Icons_Count": "0",
            "_EditNodeCreateLayers_Count": str(len(self.layers)),
        }
        for i, layer in enumerate(self.layers):
            for key, value in layer.items():
                s[f"_EditNodeCreateLayers_x005B_{i}_x005D__{key}"] = value
        s["_dw_LayerCustomColors_Count"] = "0"
        self.settings = s
        self._raw_xml = None
        self._raw = None


class ApplyAttributes(DataSetCommand):
    """Apply attributes and optionally change entity color on selected entities.
    VERIFIED (payload captured from a hand-made sample, Suite 2024.2,
    docs/samples/ApplyAttributes.txt).

    Use add_attribute(name, value="...", type="String", ...) to define
    attributes to apply, then to_entry().
    """

    name = "ApplyAttributes"

    ATTR_DEFAULTS = {
        "ChangeValue": "True",
        "Value": "",
        "Type": "String",
        "Display": "True",
        "Group": "",
        "Prompt": "False",
        "ValuesList": "",
        "LimitToList": "False",
        "Description": "",
        "Format": "",
        "Required": "False",
        "Locked": "False",
        "WeightField": "",
        "OtherDisplay": "0",
        "ChangeType": "False",
        "ChangeDisplay": "False",
        "ChangeGroup": "False",
        "ChangePrompt": "False",
        "ChangeValuesList": "False",
        "ChangeLimitToList": "False",
        "ChangeDescription": "False",
        "ChangeFormat": "False",
        "ChangeRequired": "False",
        "ChangeLocked": "False",
        "ChangeOtherDisplay": "False",
    }

    def __init__(self):
        super().__init__()
        self.attributes: list[dict[str, str]] = []

    def add_attribute(
        self,
        name: str,
        value: str = "",
        attr_type: str = "String",
        display: bool = True,
        **overrides: str,
    ) -> None:
        """Add an attribute to apply to selected entities.

        Args:
            name: attribute field name (e.g. "name", "RingNumber")
            value: default value to assign (leave blank to prompt user)
            attr_type: "String" (default) or other Deswik types
            display: whether the attribute should appear in the Attributes panel
            **overrides: any ATTR_DEFAULTS key to override
        """
        attr = dict(self.ATTR_DEFAULTS)
        attr["Name"] = name
        attr["Value"] = value
        attr["Type"] = attr_type
        attr["Display"] = "True" if display else "False"
        for key, val in overrides.items():
            attr[key] = str(val)
        self.attributes.append(attr)
        self._rebuild()

    def _rebuild(self) -> None:
        s = {
            "ApplyToSpecifiedLayer": "False",
            "ChangeColor": "False",
            "Version": "2",
            "Color": "-1",
            "NewColor_IsByBlock": "False",
            "NewColor_IsByLayer": "False",
            "NewColor_IsByColorIndex": "True",
            "NewColor_ColorIndex": "6",
            "NewColor_SystemColor": "-1",
            "NewColor_Transparency": "255",
            "NewColor_KeepColorInLayouts": "False",
            "PromptString": "Select entities",
            "EntitiesToApplyTo": "0",
            "AttributeCount": str(len(self.attributes)),
        }
        for i, attr in enumerate(self.attributes):
            for key, val in attr.items():
                s[f"Attributes__x005B_{i}_x005D__{key}"] = val
        self.settings = s
        self._raw_xml = None
        self._raw = None


class SelectEntities(DataSetCommand):
    """Prompt the user (or run automatically) to select entities for later
    commands to act on, optionally restricted to given layers/entity types.
    VERIFIED (schema captured from real Deswik.CAD process maps;
    docs/samples/SelectEntities.txt).

    Use configure(...) for the top-level options and add_layer() /
    add_entity_type() / add_external_file() to restrict the selection,
    then to_entry().
    """

    name = "SelectEntities"

    DEFAULTS = {
        "Action": "0",
        "LimitToEntities": "False",
        "EntitySelectionPrompt": "Please select entities",
        "ErrorOnNoEntities": "False",
        "ErrorMessage": "No entities selected",
        "ForceSelection": "False",
        "EntitySelection": "1",
        "FiltersUse": "False",
        "FilterString": "&lt;No Filtering&gt;",
    }

    def __init__(self):
        super().__init__()
        self.options = dict(self.DEFAULTS)
        self.layers: list[str] = []
        self.entity_types: list[str] = []
        self.external_files: list[str] = []
        self._rebuild()

    def configure(self, **overrides: str) -> None:
        """Override any DEFAULTS key, e.g. EntitySelectionPrompt=..."""
        for key, value in overrides.items():
            self.options[key] = str(value)
        self._rebuild()

    def add_layer(self, layer_path: str) -> None:
        self.layers.append(layer_path)
        self._rebuild()

    def add_entity_type(self, type_name: str) -> None:
        self.entity_types.append(type_name)
        self._rebuild()

    def add_external_file(self, path: str) -> None:
        self.external_files.append(path)
        self._rebuild()

    def _rebuild(self) -> None:
        prefix = "_dw_EditNodeSelectEntities_"
        o = self.options
        s = {
            prefix + "Action": o["Action"],
            prefix + "LimitToEntities": o["LimitToEntities"],
            prefix + "EntitySelectionPrompt": o["EntitySelectionPrompt"],
            prefix + "ErrorOnNoEntities": o["ErrorOnNoEntities"],
            prefix + "ErrorMessage": o["ErrorMessage"],
            prefix + "ForceSelection": o["ForceSelection"],
            prefix + "EntitySelection": o["EntitySelection"],
            prefix + "Layers_Count": str(len(self.layers)),
        }
        for i, layer in enumerate(self.layers):
            s[f"{prefix}Layers__x005B_{i}_x005D_"] = layer
        s[prefix + "FiltersUse"] = o["FiltersUse"]
        s[prefix + "FilterString"] = o["FilterString"]
        s[prefix + "ExternalFiles_Count"] = str(len(self.external_files))
        for i, path in enumerate(self.external_files):
            s[f"{prefix}ExternalFiles__x005B_{i}_x005D_"] = path
        s[prefix + "EntityTypes_Count"] = str(len(self.entity_types))
        for i, etype in enumerate(self.entity_types):
            s[f"{prefix}EntityTypes__x005B_{i}_x005D_"] = etype
        self.settings = s
        self._raw_xml = None
        self._raw = None


class AttributesValidate(DataSetCommand):
    """Validate and repair attribute values on selected layers: check
    required/data-type/limited-values-list rules, drop attributes not on
    the allowed list, and ensure a set of attributes always exists.
    VERIFIED (schema captured from real Deswik.CAD process maps;
    docs/samples/AttributesValidate.txt).

    Use add_layer(), add_attribute(), add_required_attribute() to build the
    rule, then to_entry().
    """

    name = "AttributesValidate"

    DEFAULTS = {
        "CheckDataTypes": "False",
        "CheckInLimitedValuesList": "False",
        "CheckRequired": "True",
        "LimitToAttributesList": "True",
        "DeleteOtherAttributes": "True",
        "AutoCorrectErrors": "False",
    }

    def __init__(self):
        super().__init__()
        self.options = dict(self.DEFAULTS)
        self.layers: list[str] = []
        self.attributes: list[str] = []
        self.required_attributes: list[str] = []
        self._rebuild()

    def configure(self, **overrides: str) -> None:
        """Override any DEFAULTS key, e.g. CheckDataTypes=True."""
        for key, value in overrides.items():
            self.options[key] = str(value)
        self._rebuild()

    def add_layer(self, layer_path: str) -> None:
        self.layers.append(layer_path)
        self._rebuild()

    def add_attribute(self, name: str) -> None:
        """Attribute allowed to remain (subject to LimitToAttributesList)."""
        self.attributes.append(name)
        self._rebuild()

    def add_required_attribute(self, name: str) -> None:
        """Attribute that must exist on the layer (EnsureAttributesExist)."""
        self.required_attributes.append(name)
        self._rebuild()

    def _rebuild(self) -> None:
        o = self.options
        s = {"LayersList_Count": str(len(self.layers))}
        for i, layer in enumerate(self.layers):
            s[f"LayersList__x005B_{i}_x005D_"] = layer
        s["CheckDataTypes"] = o["CheckDataTypes"]
        s["CheckInLimitedValuesList"] = o["CheckInLimitedValuesList"]
        s["CheckRequired"] = o["CheckRequired"]
        s["LimitToAttributesList"] = o["LimitToAttributesList"]
        s["AttributesList_Count"] = str(len(self.attributes))
        for i, attr in enumerate(self.attributes):
            s[f"AttributesList__x005B_{i}_x005D_"] = attr
        s["DeleteOtherAttributes"] = o["DeleteOtherAttributes"]
        s["AutoCorrectErrors"] = o["AutoCorrectErrors"]
        s["EnsureAttributesExist_Count"] = str(len(self.required_attributes))
        for i, attr in enumerate(self.required_attributes):
            s[f"EnsureAttributesExist__x005B_{i}_x005D_"] = attr
        self.settings = s
        self._raw_xml = None
        self._raw = None


BulkExport = _dataset("BulkExport", "Rules to export multiple entities to varying file formats.")
CommandRecording = _dataset("CommandRecording", "Recorded Deswik.CAD command processes saved on the node.")
CropEntitiesBulk = _dataset("CropEntitiesBulk", "Crop multiple entities by closed polylines using rules.")
DeleteEntitiesAndLayers = _dataset("DeleteEntitiesAndLayers", "Delete layers/entities, optionally filtered.")
DrawingDefaults = _dataset("DrawingDefaults", "Define drawing defaults applied to entities.")
Formula = _dataset("Formula", "Modify entity attributes using formula rules. (Doc title: Formulae)")
LayerPreset = _dataset("LayerPreset", "Layer presets: bulk change visibility, filters, legends.")
MoveToLayers = _dataset("MoveToLayers", "Filter-based rules to move/copy entities to layers. (Doc title: Move Or Copy To Layers)")
PromptUser = _dataset("PromptUser", "Interactive dialog displayed when the node is clicked.")
RenameAttributes = _dataset("RenameAttributes", "Rename, regroup, or delete attributes.")
SelectLayers = _dataset("SelectLayers", "Layer selection dialog or rule; stores result for later commands.")


# --------------------------------------------------------------------------
# UNVERIFIED stubs from help docs (internal names guessed from doc titles)
#
# Cross-checked against the ucEditNode*/EditNode* node-editor classes in
# Deswik.Graphics.WorkflowEditor (Deswik.Graphics.dll, Deswik.Suite 2025.2;
# inspected with dnfile per KNOWLEDGE.md Sec.8). That confirms or corrects
# the Tag `Name:` identifier only -- payload structure (DataSet columns)
# is still unconfirmed for every command below: those editor classes have
# obfuscated field names (KNOWLEDGE.md Sec.8), so a real captured sample
# is still required (see the scaling workflow in CLAUDE.md) before any of
# these can be promoted to VERIFIED.
# --------------------------------------------------------------------------

# Name confirmed: a ucEditNode<Name> (or EditNode<Name>) editor class exists
# with exactly this name in Deswik.Graphics.dll 2025.2.
_NAME_CONFIRMED = {
    "Aggregation": "Open Deswik.Agg with saved Batch/Scenario settings.",
    "BatchInterrogation": "Interrogate solids against geological models.",
    "BulkBooleanSolids": "Multiple Boolean operations in sequence.",
    "Conglomeration": "Merge solids using a selected aggregation method.",
    "CutBlocksAndBenches": "Cut solids by fixed height using a grid.",
    "CutBySurfaces": "Cut solids into horizons using surface stacking rules.",
    "DMCommands": "Alter Datamine tables using rules.",
    "DrillholeDesurvey": "Run a desurvey settings file to create drillholes.",
    "DrillholeWarning": "Generate drill-influence warning solids.",
    "EnableUndo": "Toggle undo functionality for performance.",
    "GMDLBCommands": "Alter block geomodel (*.gmdlb) tables using rules.",
    "GMDLSCommands": "Alter solid geomodel (*.gmdls) tables using rules.",
    "GridManipulation": "Create Vulcan grid files.",
    "ImportDatamineDrillholes": "Import Datamine drillhole files.",
    "InsertBlock": "Insert block entities with predefined properties.",
    "InteractiveFilter": "Temporary interactive filter on layers.",
    "LayerTreeVisibility": "Select which layers show in the layer tree.",
    "PlaneDefinition": "Create a plane definition from the current view/working plane.",
    "Plugin": "Start a plugin from the process map.",
    "PolylineOffset": "Offset polyline segments by distance on the same plane.",
    "PolylineProjection": "Project polyline vertex z-coordinates.",
    "PolylineVertexAttributes": "Digitize/assign attributes to polyline vertices.",
    "PrintBatch": "Print layouts from multiple Deswik.CAD files.",
    "RegisterToSolids": "Register entity vertices to solids/surfaces.",
    "SnapOptions": "Update snap mode settings in the active document.",
    "SolidsBooleanRelative": "Boolean solids relative operation.",
    "StopeOptimizer": "Open Deswik.SO with saved scenario settings.",
    "StripRatioCalculation": "Create and assign strip ratio attributes.",
    "ZoneInterrogation": "Generate influence zones around polylines and interrogate.",
}

# Name corrected: the doc-title guess didn't match any editor class; renamed
# to the class that does. Value is (doc, guessed_name_it_replaces).
_NAME_CORRECTED = {
    "AttributesFromGrid": ("Assign attributes from a governing polygon grid.", "AttributesFromPolygonGrid"),
    "AutoDevelopmentDesigner": ("Rule sets to bulk generate/manipulate polylines and solids.", "AutoDesigner"),
    "AutoPitDesigner": ("Generate pit designs from optimization shells.", "StrategicPitDesigner"),
    "CutBlocksSurfacesAndBenches": ("Cut solids via combined commands.", "CutBlocksBenchesAndSurfaces"),
    "DraglineDozer": ("Store dragline/dozer section settings.", "DraglineDozerSectionSettings"),
    "ExtractEntities": ("Generate solids/slices/hulls from a *.gmdls geomodel.", "ExtractEntitiesFromSolidModel"),
    "ExtractSurfaces": ("Generate roof/floor surfaces from a *.gmdls geomodel.", "ExtractSurfacesFromSolidModel"),
    "GlobalConstants": ("Edit global constants from the node.", "EditGlobalConstants"),
    "ImportAcquireDrillholes": ("Import drillholes from an acQuire database.", "ImportAcQuireDrillholes"),
    "ISBatchUpdateAttributes": ("Update attributes between graphics and Deswik.Sched tasks.", "DeswikISBatchUpdateAttributes"),
    "ISRunScheduler": ("Open Deswik.Sched and run a node without UI.", "DeswikISRunScheduler"),
    "PluginMenuCommand": ("Run a command from Deswik.IS/LHS/OPDB/OPSTS.", "ModuleMenuCommand"),
    "Transformation": ("Coordinate transformation rule sets for import/export.", "TransformationRules"),
}

# No match: the doc-title guess has no corresponding editor class in
# Deswik.Graphics.dll 2025.2 -- name is still just a guess (CopyLayers'
# functionality may in fact be folded into MoveToLayers, whose doc title
# is "Move Or Copy To Layers"; the other four may use a generic dialog with
# no dedicated ucEditNode* class, or not exist as of 2025.2).
_UNVERIFIED = {
    "CopyLayers": "Apply copy rules to selected layers.",
    "ExecuteNodes": "Run multiple nodes from a single node.",
    "ExecuteNodesWithCondition": "Run multiple nodes if conditions are met.",
    "ExternalMacro": "Associate an external macro file with the node.",
    "Validation": "Validate node statuses, layers, and global constants.",
    "VersionCheck": "Require a minimum Deswik.CAD version to run the node.",
}

_unverified_classes = {}
for _name, _doc in _NAME_CONFIRMED.items():
    _unverified_classes[_name] = type(
        _name,
        (DataSetCommand,),
        {
            "name": _name,
            "__doc__": _doc + (
                f" UNVERIFIED: Tag name confirmed against the ucEditNode{_name} "
                "editor class in Deswik.Graphics.dll (Suite 2025.2); payload "
                "structure not yet captured from a real sample -- assumed DataSet."
            ),
        },
    )
for _name, (_doc, _old) in _NAME_CORRECTED.items():
    _unverified_classes[_name] = type(
        _name,
        (DataSetCommand,),
        {
            "name": _name,
            "__doc__": _doc + (
                f" UNVERIFIED: Tag name corrected from guessed '{_old}' to "
                f"'{_name}', matching the ucEditNode{_name} editor class in "
                "Deswik.Graphics.dll (Suite 2025.2); payload structure not yet "
                "captured from a real sample -- assumed DataSet."
            ),
        },
    )
for _name, _doc in _UNVERIFIED.items():
    _unverified_classes[_name] = type(
        _name,
        (DataSetCommand,),
        {
            "name": _name,
            "__doc__": _doc + (
                " UNVERIFIED: internal Tag name guessed from doc title; no "
                "corresponding editor class found in Deswik.Graphics.dll "
                "(Suite 2025.2) to confirm it; payload style assumed DataSet."
            ),
        },
    )
globals().update(_unverified_classes)


# --------------------------------------------------------------------------
# Registry
# --------------------------------------------------------------------------

VERIFIED_COMMANDS = {
    cls.name: cls
    for cls in [
        MenuCommand, OpenProcessMap, EmbeddedMacro, ExecuteFile, CreateLayers,
        ApplyAttributes, SelectEntities, AttributesValidate,
        DisplayProcessMapLayer, SetNodeStatus, DocumentSettings, NodeStatus,
        ApplyLayout, JsonCommandList,
        MessageBox,
        BulkExport, CommandRecording, CropEntitiesBulk, DeleteEntitiesAndLayers,
        DrawingDefaults, Formula, LayerPreset, MoveToLayers, PromptUser,
        RenameAttributes, SelectLayers,
    ]
}

COMMAND_CLASSES = {**{c.name: c for c in _unverified_classes.values()}, **VERIFIED_COMMANDS}

# Confidence tier per command name, finer-grained than the verified/unverified
# split above:
#   "verified"    -- payload captured from a real sample (VERIFIED_COMMANDS)
#   "confirmed"   -- Tag name matches a ucEditNode<Name> class in
#                    Deswik.Graphics.dll (Suite 2025.2); payload still guessed
#   "corrected"   -- doc-title guess was wrong; renamed to match the DLL's
#                    ucEditNode<Name> class; payload still guessed
#   "unconfirmed" -- no corresponding editor class found in the DLL at all
COMMAND_CONFIDENCE = {
    **{name: "unconfirmed" for name in _UNVERIFIED},
    **{name: "corrected" for name in _NAME_CORRECTED},
    **{name: "confirmed" for name in _NAME_CONFIRMED},
    **{name: "verified" for name in VERIFIED_COMMANDS},
}

# Functional area per command name, for grouping the GUI's command picker.
# Every Tag name traces back to the same DLL (Deswik.Graphics.dll), so
# grouping by DLL would collapse to one bucket -- this groups by what the
# command actually does in Deswik instead.
COMMAND_CATEGORY = {
    # Layers & Attributes
    "ApplyAttributes": "Layers & Attributes",
    "AttributesFromGrid": "Layers & Attributes",
    "AttributesValidate": "Layers & Attributes",
    "CopyLayers": "Layers & Attributes",
    "CreateLayers": "Layers & Attributes",
    "DeleteEntitiesAndLayers": "Layers & Attributes",
    "Formula": "Layers & Attributes",
    "InteractiveFilter": "Layers & Attributes",
    "LayerPreset": "Layers & Attributes",
    "LayerTreeVisibility": "Layers & Attributes",
    "MoveToLayers": "Layers & Attributes",
    "RenameAttributes": "Layers & Attributes",
    "SelectLayers": "Layers & Attributes",
    # Geometry & Solids
    "BulkBooleanSolids": "Geometry & Solids",
    "Conglomeration": "Geometry & Solids",
    "CropEntitiesBulk": "Geometry & Solids",
    "CutBlocksAndBenches": "Geometry & Solids",
    "CutBlocksSurfacesAndBenches": "Geometry & Solids",
    "CutBySurfaces": "Geometry & Solids",
    "DrawingDefaults": "Geometry & Solids",
    "InsertBlock": "Geometry & Solids",
    "PlaneDefinition": "Geometry & Solids",
    "PolylineOffset": "Geometry & Solids",
    "PolylineProjection": "Geometry & Solids",
    "PolylineVertexAttributes": "Geometry & Solids",
    "RegisterToSolids": "Geometry & Solids",
    "SelectEntities": "Geometry & Solids",
    "SnapOptions": "Geometry & Solids",
    "SolidsBooleanRelative": "Geometry & Solids",
    # Geology & Geomodels
    "BatchInterrogation": "Geology & Geomodels",
    "DMCommands": "Geology & Geomodels",
    "DrillholeDesurvey": "Geology & Geomodels",
    "DrillholeWarning": "Geology & Geomodels",
    "ExtractEntities": "Geology & Geomodels",
    "ExtractSurfaces": "Geology & Geomodels",
    "GMDLBCommands": "Geology & Geomodels",
    "GMDLSCommands": "Geology & Geomodels",
    "GridManipulation": "Geology & Geomodels",
    "ZoneInterrogation": "Geology & Geomodels",
    # Design & Optimization
    "AutoDevelopmentDesigner": "Design & Optimization",
    "AutoPitDesigner": "Design & Optimization",
    "DraglineDozer": "Design & Optimization",
    "StripRatioCalculation": "Design & Optimization",
    # External Tools & Integration
    "Aggregation": "External Tools & Integration",
    "ApplyLayout": "External Tools & Integration",
    "ISBatchUpdateAttributes": "External Tools & Integration",
    "ISRunScheduler": "External Tools & Integration",
    "Plugin": "External Tools & Integration",
    "PluginMenuCommand": "External Tools & Integration",
    "StopeOptimizer": "External Tools & Integration",
    # Import, Export & Reporting
    "BulkExport": "Import, Export & Reporting",
    "ImportAcquireDrillholes": "Import, Export & Reporting",
    "ImportDatamineDrillholes": "Import, Export & Reporting",
    "PrintBatch": "Import, Export & Reporting",
    "Transformation": "Import, Export & Reporting",
    # Node & Workflow Control
    "Command": "Node & Workflow Control",
    "CommandRecording": "Node & Workflow Control",
    "DisplayProcessMapLayer": "Node & Workflow Control",
    "DocumentSettings": "Node & Workflow Control",
    "EmbeddedMacro": "Node & Workflow Control",
    "EnableUndo": "Node & Workflow Control",
    "ExecuteFile": "Node & Workflow Control",
    "ExecuteNodes": "Node & Workflow Control",
    "ExecuteNodesWithCondition": "Node & Workflow Control",
    "ExternalMacro": "Node & Workflow Control",
    "GlobalConstants": "Node & Workflow Control",
    "JsonCommandList": "Node & Workflow Control",
    "MessageBox": "Node & Workflow Control",
    "NodeStatus": "Node & Workflow Control",
    "PromptUser": "Node & Workflow Control",
    "SetNodeStatus": "Node & Workflow Control",
    "Validation": "Node & Workflow Control",
    "VersionCheck": "Node & Workflow Control",
    "Workflow": "Node & Workflow Control",
}

# Fixed display order for the GUI's command-picker groups.
COMMAND_CATEGORY_ORDER = [
    "Layers & Attributes",
    "Geometry & Solids",
    "Geology & Geomodels",
    "Design & Optimization",
    "External Tools & Integration",
    "Import, Export & Reporting",
    "Node & Workflow Control",
]


def resolve(entry: CommandEntry):
    """Return a typed command object for a CommandEntry (GenericCommand if
    the command name is unknown)."""
    cls = COMMAND_CLASSES.get(entry.name, GenericCommand)
    if cls is GenericCommand:
        return GenericCommand.from_entry(entry)
    return cls.from_entry(entry)
