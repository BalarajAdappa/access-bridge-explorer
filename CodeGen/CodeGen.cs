﻿// Copyright 2016 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CodeGen.Definitions;
using CodeGen.Interop;
using CodeGen.Interop.NativeStructures;
using WindowsAccessBridgeDefinition = CodeGen.Interop.WindowsAccessBridgeDefinition;

namespace CodeGen {
  public class CodeGen {
    private readonly string _outputFilename;

    public CodeGen(string outputFilename) {
      _outputFilename = outputFilename;
    }

    public void Generate() {
      var model = new WindowsAccessBridgeModelCollector().CollectModel();
      WriteFile(model);
    }

    private void WriteFile(WindowsAccessBridgeModel model) {
      using (var writer = File.CreateText(_outputFilename)) {
        using (var sourceWriter = new SourceCodeWriter(writer, model)) {
          sourceWriter.WriteLine("// Copyright 2016 Google Inc. All Rights Reserved.");
          sourceWriter.WriteLine("// ");
          sourceWriter.WriteLine("// Licensed under the Apache License, Version 2.0 (the \"License\");");
          sourceWriter.WriteLine("// you may not use this file except in compliance with the License.");
          sourceWriter.WriteLine("// You may obtain a copy of the License at");
          sourceWriter.WriteLine("// ");
          sourceWriter.WriteLine("//     http://www.apache.org/licenses/LICENSE-2.0");
          sourceWriter.WriteLine("// ");
          sourceWriter.WriteLine("// Unless required by applicable law or agreed to in writing, software");
          sourceWriter.WriteLine("// distributed under the License is distributed on an \"AS IS\" BASIS,");
          sourceWriter.WriteLine("// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.");
          sourceWriter.WriteLine("// See the License for the specific language governing permissions and");
          sourceWriter.WriteLine("// limitations under the License.");
          sourceWriter.WriteLine();

          sourceWriter.WriteLine("// ReSharper disable InconsistentNaming");
          sourceWriter.WriteLine("// ReSharper disable DelegateSubtraction");
          sourceWriter.WriteLine("// ReSharper disable UseObjectOrCollectionInitializer");
          sourceWriter.WriteLine("// ReSharper disable UnusedParameter.Local");
          sourceWriter.WriteLine("// ReSharper disable UnusedMember.Local");
          sourceWriter.WriteLine("// ReSharper disable ConvertIfStatementToConditionalTernaryExpression");

          sourceWriter.AddUsing("System");
          sourceWriter.AddUsing("System.Runtime.InteropServices");
          sourceWriter.AddUsing("System.Text");
          sourceWriter.AddUsing("WindowHandle = System.IntPtr");
          sourceWriter.AddUsing("BOOL = System.Int32");
          sourceWriter.WriteLine();

          sourceWriter.StartNamespace("AccessBridgeExplorer.WindowsAccessBridge");
          sourceWriter.IsNativeTypes = false;
          WriteApplicationFunctionsInterface(model, sourceWriter);
          WriteApplicationEventsInterface(model, sourceWriter);
          WriteApplicationEventHandlerTypes(model, sourceWriter);
          WriteApplicationEnums(model, writer, sourceWriter);
          WriteApplicationStructs(model, sourceWriter, writer);
          WriteApplicationClasses(model, writer, sourceWriter);

          foreach (var legacy in new[] {false, true}) {
            sourceWriter.IsLegacy = legacy;
            WriteApplicationFunctionsClass(model, sourceWriter);
            WriteApplicationEventsClass(model, sourceWriter);
          }

          sourceWriter.IsNativeTypes = true;
          foreach (var legacy in new[] {false, true}) {
            sourceWriter.IsLegacy = legacy;
            WriteLibraryFunctionsClass(model, sourceWriter);
          }
          foreach (var legacy in new[] { false, true }) {
            sourceWriter.IsLegacy = legacy;
            WriteLibraryEventsClass(model, sourceWriter);
          }
          foreach (var legacy in new[] { false, true }) {
            sourceWriter.IsLegacy = legacy;
            WriteLibraryStructs(model, sourceWriter, writer);
            WriteLibraryClasses(model, writer, sourceWriter);
          }
          sourceWriter.EndNamespace();
        }
      }
    }

    private void WriteApplicationEnums(WindowsAccessBridgeModel model, StreamWriter writer, SourceCodeWriter sourceWriter) {
      model.Enums.ForEach(x => {
        WriteEnum(model, sourceWriter, x);
        writer.WriteLine();
      });
    }

    private void WriteLibraryStructs(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, StreamWriter writer) {
      sourceWriter.IsNativeTypes = true;
      model.Structs.ForEach(x => {
        WriteLibraryStruct(model, sourceWriter, x);
        writer.WriteLine();
      });
    }

    private void WriteLibraryClasses(WindowsAccessBridgeModel model, StreamWriter writer, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = true;
      model.Classes.ForEach(x => {
        WriteLibraryClass(model, sourceWriter, x);
        writer.WriteLine();
      });
    }

    private void WriteApplicationStructs(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, StreamWriter writer) {
      sourceWriter.IsNativeTypes = false;
      model.Structs.ForEach(x => {
        WriteApplicationStruct(model, sourceWriter, x);
        writer.WriteLine();
      });
    }

    private void WriteApplicationClasses(WindowsAccessBridgeModel model, StreamWriter writer, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      model.Classes.ForEach(x => {
        WriteApplicationClass(model, sourceWriter, x);
        writer.WriteLine();
      });
    }

    private void WriteApplicationFunctionsInterface(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Platform agnostic abstraction over WindowAccessBridge DLL functions");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public interface IAccessBridgeFunctions {{");
      sourceWriter.IncIndent();
      foreach (var function in model.Functions) {
        WriteFunction(model, sourceWriter, function);
      }
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationEventsInterface(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Platform agnostic abstraction over WindowAccessBridge DLL events");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public interface IAccessBridgeEvents : IDisposable {{");
      sourceWriter.IncIndent();
      foreach (var eventDefinition in model.Events) {
        WriteEvent(sourceWriter, eventDefinition);
      }
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationEventHandlerTypes(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("#region Platform agnostic event handler delegate types");
      foreach (var eventDefinition in model.Events) {
        WriteEventHandlerType(model, sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationFunctionsClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Implementation of platform agnostic functions");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public partial class AccessBridgeFunctions{0} : IAccessBridgeFunctions {{", GetLegacySuffix(sourceWriter));
      sourceWriter.IncIndent();

      sourceWriter.WriteLine();
      sourceWriter.WriteLine("#region Function implementations");
      sourceWriter.WriteLine();
      foreach (var function in model.Functions) {
        WriteApplicationFunctionImplementation(model, sourceWriter, function);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Wrap/Unwrap structs");
      sourceWriter.WriteLine();
      foreach (var definition in model.Structs) {
        WriteApplicationWrapStructFunctions(model, sourceWriter, definition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region CopyWrap/CopyUnwrap classes");
      sourceWriter.WriteLine();
      foreach (var definition in model.Classes) {
        WriteApplicationCopyClassFunctions(model, sourceWriter, definition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationEventsClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Implementation of platform agnostic events");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public partial class AccessBridgeEvents{0} : IAccessBridgeEvents {{", GetLegacySuffix(sourceWriter));
      sourceWriter.IncIndent();

      sourceWriter.WriteLine("#region Event fields");
      foreach (var eventDefinition in model.Events) {
        WriteApplicationLevelEventField(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event properties");
      foreach (var eventDefinition in model.Events) {
        WriteApplicationLevelEventProperty(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event handlers");
      foreach (var eventDefinition in model.Events) {
        WriteApplicationLevelEventHandler(model, sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("private void DetachForwarders() {{");
      sourceWriter.IncIndent();
      foreach (var eventDefinition in model.Events) {
        sourceWriter.WriteLine("NativeEvents.{0} -= Forward{0};", eventDefinition.Name);
      }
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event forwarders");
      foreach (var eventDefinition in model.Events) {
        WriteApplicationLevelEventForwarder(model, sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");

      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteLibraryFunctionsClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Container of WindowAccessBridge DLL entry points");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public class AccessBridgeLibraryFunctions{0} {{", GetLegacySuffix(sourceWriter));
      sourceWriter.IncIndent();

      sourceWriter.WriteLine("#region Functions");
      foreach (var function in model.Functions) {
        WriteLibraryFunctionProperty(sourceWriter, function);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event functions");
      foreach (var eventDefinition in model.Events) {
        WriteLibraryEventProperty(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Function delegate types");
      foreach (var function in model.Functions) {
        WriteLibrayrFunctionsDelegate(model, sourceWriter, function);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event delegate types");
      foreach (var definition in model.Events) {
        sourceWriter.WriteLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]");
        WriteDelegateType(model, sourceWriter, definition.DelegateFunction);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event function delegate types");
      foreach (var eventDefinition in model.Events) {
        sourceWriter.WriteLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]");
        WriteLibraryEventDelegateType(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");

      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    public string GetLegacySuffix(SourceCodeWriter sourceWriter) {
        return sourceWriter.IsLegacy ? "Legacy" : ""; 
    }

    private void WriteLibraryEventsClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter) {
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteLine("/// <summary>");
      sourceWriter.WriteLine("/// Native library event handlers implementation");
      sourceWriter.WriteLine("/// </summary>");
      sourceWriter.WriteLine("public partial class AccessBridgeEventsNative{0} {{", GetLegacySuffix(sourceWriter));
      sourceWriter.IncIndent();

      sourceWriter.WriteLine("#region Event fields");
      foreach (var eventDefinition in model.Events) {
        WriteNativeEventField(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event delegate fields");
      foreach (var eventDefinition in model.Events) {
        WriteNativeEventDelegateField(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();

      sourceWriter.WriteLine("#region Event properties");
      foreach (var eventDefinition in model.Events) {
        WriteNativeEventProperty(sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.WriteLine();
      sourceWriter.WriteLine("#region Event handlers");
      foreach (var eventDefinition in model.Events) {
        WriteNativeEventHandler(model, sourceWriter, eventDefinition);
      }
      sourceWriter.WriteLine("#endregion");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteFunction(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition definition) {
      sourceWriter.WriteIndent();
      WriteFunctionSignature(model, sourceWriter, definition);
      sourceWriter.Write(";");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationFunctionImplementation(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("public ");
      WriteFunctionSignature(model, sourceWriter, definition);
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();

      //
      // Generate temporary local variables for out/ref parameters.
      //
      var javaOutObjects = definition.Parameters.Where(p => p.IsOut && IsJavaObjectHandle(p.Type)).ToList();
      var javaInObjects = definition.Parameters.Where(p => !p.IsOut && IsJavaObjectHandle(p.Type)).ToList();
      foreach (var x in javaOutObjects) {
        sourceWriter.WriteIndent();
        sourceWriter.IsNativeTypes = true;
        sourceWriter.WriteType(x.Type);
        sourceWriter.IsNativeTypes = false;
        sourceWriter.Write(" {0}Temp;", x.Name);
        sourceWriter.WriteLine();
      }

      var outStructs = definition.Parameters.Where(p => (p.IsOut || p.IsRef || p.IsOutAttribute) && model.IsStruct(p.Type)).ToList();
      foreach (var x in outStructs) {
        sourceWriter.WriteIndent();
        sourceWriter.IsNativeTypes = true;
        sourceWriter.WriteType(x.Type);
        if (x.IsRef)
          sourceWriter.Write(" {0}Temp = Unwrap(vmid, {0});", x.Name);
        else
          sourceWriter.Write(" {0}Temp;", x.Name);
        sourceWriter.WriteLine();
        sourceWriter.IsNativeTypes = false;
      }
      var outClasses = definition.Parameters.Where(p => (p.IsOut || p.IsOutAttribute) && model.IsClass(p.Type)).ToList();
      foreach (var x in outClasses) {
        sourceWriter.WriteIndent();
        sourceWriter.IsNativeTypes = true;
        sourceWriter.WriteType(x.Type);
        sourceWriter.Write(" {0}Temp = new {1}();", x.Name, sourceWriter.GetTypeName(x.Type));
        sourceWriter.WriteLine();
        sourceWriter.IsNativeTypes = false;
      }

      //
      // Generate function call
      //
      sourceWriter.WriteIndent();
      if (!IsVoid(definition.ReturnType)) {
        sourceWriter.Write("var result = ");
      }
      sourceWriter.Write("LibraryFunctions.{0}(", definition.Name);
      var first = true;
      foreach (var p in definition.Parameters) {
        if (first)
          first = false;
        else
          sourceWriter.Write(", ");
        if (p.IsOut)
          sourceWriter.Write("out ");
        else if (p.IsRef)
          sourceWriter.Write("ref ");
        if (IsJavaObjectHandle(p.Type)) {
          if (p.IsOut)
            sourceWriter.Write("{0}Temp", p.Name);
          else
            sourceWriter.Write("Unwrap(vmid, {0})", p.Name);
        } else if (outStructs.Contains(p) || outClasses.Contains(p)) {
          sourceWriter.Write("{0}Temp", p.Name);
        } else {
          sourceWriter.Write(p.Name);
        }
      }
      sourceWriter.Write(");");
      sourceWriter.WriteLine();

      //
      // Generate code to keep handles alive for GC.
      //
      foreach (var x in javaInObjects) {
        sourceWriter.WriteLine("GC.KeepAlive({0});", x.Name);
      }

      //
      // Generate code to copy back temporary local variables to ref/out parameters.
      //
      foreach (var x in javaOutObjects) {
        var wrapExpression = string.Format("{0} = Wrap(vmid, {0}Temp)", x.Name);
        if (IsStatusResult(definition.ReturnType)) {
          sourceWriter.WriteLine("if (Succeeded(result)) {{");
          sourceWriter.IncIndent();
          sourceWriter.WriteLine("{0};", wrapExpression);
          sourceWriter.DecIndent();
          sourceWriter.WriteLine("}} else {{");
          sourceWriter.IncIndent();
          sourceWriter.IsNativeTypes = true;
          sourceWriter.WriteLine("{0}Temp = default({1});", x.Name, sourceWriter.GetTypeName(x.Type));
          sourceWriter.IsNativeTypes = false;
          sourceWriter.WriteLine("{0};", wrapExpression);
          sourceWriter.DecIndent();
          sourceWriter.WriteLine("}}");
        } else {
          sourceWriter.WriteLine("{0};", wrapExpression);
        }
      }

      foreach (var x in outStructs) {
        var wrapExpression = string.Format("{0} = Wrap(vmid, {0}Temp)", x.Name);
        if (IsStatusResult(definition.ReturnType)) {
          sourceWriter.WriteLine("if (Succeeded(result))");
          sourceWriter.IncIndent();
          sourceWriter.WriteLine("{0};", wrapExpression);
          sourceWriter.DecIndent();
          if (x.IsOut) {
            sourceWriter.WriteLine("else");
            sourceWriter.IncIndent();
            sourceWriter.WriteLine("{0} = default({1});", x.Name, sourceWriter.GetTypeName(x.Type));
            sourceWriter.DecIndent();
          }
        } else {
          sourceWriter.WriteLine("{0};", wrapExpression);
        }
      }

      foreach (var x in outClasses) {
        if (x.IsOutAttribute && !sourceWriter.IsNativeTypes) {
          sourceWriter.WriteLine("{0} = new {1}();", x.Name, sourceWriter.GetTypeName(x.Type));
        }

        var wrapExpression = string.Format("CopyWrap(vmid, {0}Temp, {0})", x.Name);
        if (IsStatusResult(definition.ReturnType)) {
          sourceWriter.WriteLine("if (Succeeded(result))");
          sourceWriter.IncIndent();
          sourceWriter.WriteLine("{0};", wrapExpression);
          sourceWriter.DecIndent();
          if (x.IsOut) {
            sourceWriter.WriteLine("else");
            sourceWriter.IncIndent();
            sourceWriter.WriteLine("{0} = default({1});", x.Name, sourceWriter.GetTypeName(x.Type));
            sourceWriter.DecIndent();
          }
        } else {
          sourceWriter.WriteLine("{0};", wrapExpression);
        }
      }

      if (!IsVoid(definition.ReturnType)) {
        if (IsJavaObjectHandle(definition.ReturnType)) {
          sourceWriter.WriteLine("return Wrap(vmid, result);");
        } else if (IsBool(definition.ReturnType)) {
          sourceWriter.WriteLine("return ToBool(result);");
        } else if (IsStatusResult(definition.ReturnType)) {
          sourceWriter.WriteLine("return Succeeded(result);");
        } else {
          sourceWriter.WriteLine("return result;");
        }
      }

      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationWrapStructFunctions(
      WindowsAccessBridgeModel model,
      SourceCodeWriter sourceWriter,
      BaseTypeDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("private ");
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" Wrap(int vmid, ");
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" info)");
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteLine("var result = new {0}();", sourceWriter.GetTypeName(definition.Name));
      sourceWriter.IsNativeTypes = true;
      WriteCopyFields(model, sourceWriter, "info", "result", definition);
      sourceWriter.WriteLine("return result;");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();

      sourceWriter.WriteIndent();
      sourceWriter.Write("private ");
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" Unwrap(int vmid, ");
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" info)");
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteLine("var result = new {0}();", sourceWriter.GetTypeName(definition.Name));
      sourceWriter.IsNativeTypes = false;
      WriteCopyFields(model, sourceWriter, "info", "result", definition);
      sourceWriter.WriteLine("return result;");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteApplicationCopyClassFunctions(
      WindowsAccessBridgeModel model,
      SourceCodeWriter sourceWriter,
      BaseTypeDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("private void CopyWrap(int vmid, ");
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" infoSrc, ");
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" infoDest)");
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.IsNativeTypes = true;
      WriteCopyFields(model, sourceWriter, "infoSrc", "infoDest", definition);
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();

      sourceWriter.WriteIndent();
      sourceWriter.Write("private void CopyUnwrap(int vmid, ");
      sourceWriter.IsNativeTypes = false;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" infoSrc, ");
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteType(definition.Name);
      sourceWriter.Write(" infoDest)");
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.IsNativeTypes = false;
      WriteCopyFields(model, sourceWriter, "infoSrc", "infoDest", definition);
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine();
    }

    private void WriteCopyFields(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, string infosrc, string infodest, BaseTypeDefinition definition) {
      definition.Fields.ForEach(field => {
        WriteCopyField(model, sourceWriter, infosrc, infodest, "." + field.Name, field.Type);
      });
    }

    private void WriteCopyField(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, string infosrc, string infodest, string fieldName, TypeReference type) {
      if (model.IsStruct(type)) {
        sourceWriter.WriteLine("{0}{1} = {4}(vmid, {2}{3});", infodest, fieldName, infosrc, fieldName,
          sourceWriter.IsNativeTypes ? "Wrap" : "Unwrap");
      } else if (model.IsClass(type)) {
        sourceWriter.WriteLine("{4}(vmid, {0}{1}, {2}{3});", infosrc, fieldName, infodest, fieldName,
          sourceWriter.IsNativeTypes ? "CopyWrap" : "CopyUnwrap");
      } else if (IsJavaObjectHandle(type)) {
        sourceWriter.WriteLine("{0}{1} = {4}(vmid, {2}{3});", infodest, fieldName, infosrc, fieldName,
          sourceWriter.IsNativeTypes ? "Wrap" : "Unwrap");
      } else if (type is ArrayTypeReference) {
        var elementType = ((ArrayTypeReference)type).ElementType;
        var elementCountFiedName = ((ArrayTypeReference) type).ElementCountAttribute == null ? null : ((ArrayTypeReference)type).ElementCountAttribute.FieldName;
        sourceWriter.WriteLine("if ({0}{1} != null) {{", infosrc, fieldName);
        sourceWriter.IncIndent();
        if (elementCountFiedName == null) {
          sourceWriter.WriteLine("var count = {0}{1}.Length;", infosrc, fieldName);
        } else {
          sourceWriter.WriteLine("var count = {0}.{1};", infosrc, elementCountFiedName);
        }
        sourceWriter.IsNativeTypes = !sourceWriter.IsNativeTypes;
        sourceWriter.WriteLine("{0}{1} = new {2}[count];", infodest, fieldName, sourceWriter.GetTypeName(elementType));
        sourceWriter.IsNativeTypes = !sourceWriter.IsNativeTypes;
        sourceWriter.WriteLine("for(var i = 0; i < count; i++) {{");
        sourceWriter.IncIndent();
        WriteCopyField(model, sourceWriter, infosrc + fieldName, infodest + fieldName, "[i]", elementType);
        sourceWriter.DecIndent();
        sourceWriter.WriteLine("}}");
        sourceWriter.DecIndent();
        sourceWriter.WriteLine("}}");
      } else {
        sourceWriter.WriteLine("{0}{1} = {2}{3};", infodest, fieldName, infosrc, fieldName);
      }
    }

    private void WriteDelegateType(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition definition) {
      sourceWriter.WriteMarshalAsLine(definition.MarshalAs);
      sourceWriter.WriteIndent();
      sourceWriter.Write("public delegate ");
      WriteFunctionSignature(model, sourceWriter, definition);
      sourceWriter.Write(";");
      sourceWriter.WriteLine();
    }

    private void WriteFunctionSignature(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition definition) {
      WriteFunctionSignature(model, sourceWriter, definition, definition.Name);
    }

    private void WriteFunctionSignature(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition definition, string name) {
      sourceWriter.WriteType(definition.ReturnType);
      sourceWriter.Write(" ");
      sourceWriter.Write(name);
      sourceWriter.Write("(");
      bool first = true;
      foreach (var p in definition.Parameters) {
        if (first)
          first = false;
        else {
          sourceWriter.Write(", ");
        }
        WriteParameter(model, sourceWriter, p);
      }
      sourceWriter.Write(")");
    }

    private void WriteLibrayrFunctionsDelegate(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FunctionDefinition function) {
      sourceWriter.WriteLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]");
      sourceWriter.WriteIndent();
      sourceWriter.Write("public delegate ");
      sourceWriter.WriteType(function.ReturnType);
      sourceWriter.Write(" ");
      sourceWriter.Write("{0}FP", function.Name);
      sourceWriter.Write("(");
      bool first = true;
      foreach (var p in function.Parameters) {
        if (first)
          first = false;
        else {
          sourceWriter.Write(", ");
        }
        WriteParameter(model, sourceWriter, p);
      }
      sourceWriter.Write(");");
      sourceWriter.WriteLine();
    }

    private void WriteLibraryFunctionProperty(SourceCodeWriter sourceWriter, FunctionDefinition function) {
      sourceWriter.WriteLine("public {0}FP {0} {{ get; set; }}", function.Name);
    }

    private void WriteLibraryEventDelegateType(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("public delegate BOOL Set{0}FP({0}EventHandler handler)", definition.Name);
      sourceWriter.Write(";");
      sourceWriter.WriteLine();
    }

    private void WriteLibraryEventProperty(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("public Set{0}FP Set{0} {{ get; set; }}", definition.Name);
    }

    private void WriteNativeEventField(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("private AccessBridgeLibraryFunctions{0}.{1}EventHandler _{2};",
        GetLegacySuffix(sourceWriter), definition.Name, ToPascalCase(definition));
    }

    private void WriteNativeEventDelegateField(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("private AccessBridgeLibraryFunctions{0}.{1}EventHandler _on{1}KeepAliveDelegate;",
        GetLegacySuffix(sourceWriter), definition.Name);
    }

    private void WriteNativeEventProperty(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("public event AccessBridgeLibraryFunctions{0}.{1}EventHandler {1} {{",
        GetLegacySuffix(sourceWriter), definition.Name);
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("add {{");
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("if (_{0} == null) {{", ToPascalCase(definition));
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("_on{0}KeepAliveDelegate = On{0};", definition.Name);
      sourceWriter.WriteLine("LibraryFunctions.Set{0}(_on{0}KeepAliveDelegate);", definition.Name);
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.DecIndent();

      sourceWriter.IncIndent();
      sourceWriter.WriteLine("_{0} += value;", ToPascalCase(definition));
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine("remove{{");
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("_{0} -= value;", ToPascalCase(definition));
      sourceWriter.WriteLine("if (_{0} == null) {{", ToPascalCase(definition));
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("LibraryFunctions.Set{0}(null);", definition.Name);
      sourceWriter.WriteLine("_on{0}KeepAliveDelegate = null;", definition.Name);
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
    }

    private void WriteApplicationLevelEventField(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("private {0}EventHandler _{1};", definition.Name, ToPascalCase(definition));
    }

    private void WriteApplicationLevelEventProperty(SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteLine("public event {0}EventHandler {0} {{", definition.Name);
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("add {{");
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("if (_{0} == null)", ToPascalCase(definition));
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("NativeEvents.{0} += Forward{0};", definition.Name);
      sourceWriter.DecIndent();
      sourceWriter.DecIndent();

      sourceWriter.IncIndent();
      sourceWriter.WriteLine("_{0} += value;", ToPascalCase(definition));
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.WriteLine("remove{{");
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("_{0} -= value;", ToPascalCase(definition));
      sourceWriter.WriteLine("if (_{0} == null)", ToPascalCase(definition));
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("NativeEvents.{0} -= Forward{0};", definition.Name);
      sourceWriter.DecIndent();
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
    }

    private void WriteNativeEventHandler(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("protected virtual ");
      WriteFunctionSignature(model, sourceWriter, definition.DelegateFunction, "On" + definition.Name);
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("var handler = _{0};", ToPascalCase(definition));
      sourceWriter.WriteLine("if (handler != null)");
      sourceWriter.IncIndent();
      sourceWriter.WriteIndent();
      sourceWriter.Write("handler(");
      var first = true;
      foreach (var p in definition.DelegateFunction.Parameters) {
        if (first)
          first = false;
        else
          sourceWriter.Write(", ");
        sourceWriter.Write("{0}", p.Name);
      }
      sourceWriter.Write(");");
      sourceWriter.WriteLine();
      sourceWriter.DecIndent();
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
    }

    private void WriteApplicationLevelEventHandler(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.WriteIndent();
      sourceWriter.Write("protected virtual ");
      WriteFunctionSignature(model, sourceWriter, definition.DelegateFunction, "On" + definition.Name);
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      sourceWriter.IncIndent();
      sourceWriter.WriteLine("var handler = _{0};", ToPascalCase(definition));
      sourceWriter.WriteLine("if (handler != null)");
      sourceWriter.IncIndent();
      sourceWriter.WriteIndent();
      sourceWriter.Write("handler(");
      var first = true;
      foreach (var p in definition.DelegateFunction.Parameters) {
        if (first)
          first = false;
        else
          sourceWriter.Write(", ");
        sourceWriter.Write("{0}", p.Name);
      }
      sourceWriter.Write(");");
      sourceWriter.WriteLine();
      sourceWriter.DecIndent();
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
    }

    private void WriteApplicationLevelEventForwarder(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EventDefinition definition) {
      sourceWriter.IsNativeTypes = true;
      sourceWriter.WriteIndent();
      sourceWriter.Write("private ");
      WriteFunctionSignature(model, sourceWriter, definition.DelegateFunction, "Forward" + definition.Name);
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();

      sourceWriter.IsNativeTypes = false;
      sourceWriter.IncIndent();
      sourceWriter.WriteIndent();
      sourceWriter.Write("On{0}(", definition.Name);
      var first = true;
      foreach (var p in definition.DelegateFunction.Parameters) {
        if (first)
          first = false;
        else
          sourceWriter.Write(", ");
        if (IsJavaObjectHandle(p.Type)) {
          sourceWriter.Write("Wrap(vmid, {0})", p.Name);
        } else {
          sourceWriter.Write("{0}", p.Name);
        }
      }
      sourceWriter.Write(");");
      sourceWriter.WriteLine();
      sourceWriter.DecIndent();
      sourceWriter.WriteLine("}}");
    }

    private static bool IsJavaObjectHandle(TypeReference p) {
      var name = p as NameTypeReference;
      if (name == null)
        return false;
      return name.Name == typeof(JavaObjectHandle).Name;
    }

    private static bool IsStatusResult(TypeReference p) {
      var name = p as NameTypeReference;
      if (name == null)
        return false;
      return name.Name == typeof(StatusResult).Name;
    }

    private static bool IsBool(TypeReference p) {
      var name = p as NameTypeReference;
      if (name == null)
        return false;
      return name.Name == "bool";
    }

    private static bool IsVoid(TypeReference p) {
      var name = p as NameTypeReference;
      if (name == null)
        return false;
      return name.Name == "void";
    }

    private void WriteParameter(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, ParameterDefinition parameterDefinition) {
      if (parameterDefinition.IsOutAttribute) {
        if (sourceWriter.IsNativeTypes || !model.IsClass(parameterDefinition.Type))
          sourceWriter.Write("[Out]");
      }

      if (parameterDefinition.IsOut) {
        sourceWriter.Write("out ");
      } else if (parameterDefinition.IsOutAttribute && model.IsClass(parameterDefinition.Type) && !sourceWriter.IsNativeTypes) {
        sourceWriter.Write("out ");
      } else if (parameterDefinition.IsRef) {
        sourceWriter.Write("ref ");
      }
      sourceWriter.WriteMashalAs(parameterDefinition.MarshalAs);
      sourceWriter.WriteType(parameterDefinition.Type);
      sourceWriter.Write(" ");
      sourceWriter.Write(parameterDefinition.Name);
    }

    private void WriteEvent(SourceCodeWriter sourceWriter, EventDefinition eventDefinition) {
      sourceWriter.WriteLine("event {0}EventHandler {0};", eventDefinition.Name);
    }

    private void WriteEventHandlerType(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EventDefinition eventDefinition) {
      WriteDelegateType(model, sourceWriter, eventDefinition.DelegateFunction);
    }

    private void WriteLibraryStruct(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, StrucDefinition definition) {
      sourceWriter.WriteLine("[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]");
      sourceWriter.WriteLine("public struct {0}Native{1} {{", definition.Name, GetLegacySuffix(sourceWriter));
      WriteFields(model, sourceWriter, definition);
      sourceWriter.WriteLine("}}");
    }

    private void WriteLibraryClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, ClassDefinition classDefinition) {
      sourceWriter.WriteLine("[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]");
      sourceWriter.WriteLine("public class {0}Native{1} {{", classDefinition.Name, GetLegacySuffix(sourceWriter));
      WriteFields(model, sourceWriter, classDefinition);
      sourceWriter.WriteLine("}}");
    }

    private void WriteApplicationStruct(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, StrucDefinition definition) {
      sourceWriter.WriteLine("public struct {0} {{", definition.Name);
      WriteFields(model, sourceWriter, definition);
      sourceWriter.WriteLine("}}");
    }

    private void WriteApplicationClass(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, ClassDefinition classDefinition) {
      sourceWriter.WriteLine("public class {0} {{", classDefinition.Name);
      WriteFields(model, sourceWriter, classDefinition);
      sourceWriter.WriteLine("}}");
    }

    private void WriteEnum(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EnumDefinition definition) {
      if (definition.IsFlags)
        sourceWriter.WriteLine("[Flags]");
      sourceWriter.WriteIndent();
      sourceWriter.Write("public enum {0}", definition.Name);
      if (definition.Type.Name != "int") {
        sourceWriter.Write(" : ");
        sourceWriter.WriteType(definition.Type);
      }
      sourceWriter.Write(" {{");
      sourceWriter.WriteLine();
      WriteEnumMembers(model, sourceWriter, definition);
      sourceWriter.WriteLine("}}");
    }

    private void WriteFields(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, BaseTypeDefinition definition) {
      sourceWriter.IncIndent();
      definition.Fields.ForEach(f => WriteField(model, sourceWriter, f));
      sourceWriter.DecIndent();
    }

    private void WriteField(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, FieldDefinition definition) {
      sourceWriter.WriteMarshalAsLine(definition.MarshalAs);
      sourceWriter.WriteIndent();
      sourceWriter.Write("public ");
      sourceWriter.WriteType(definition.Type);
      sourceWriter.Write(" ");
      sourceWriter.Write("{0};", definition.Name);
      sourceWriter.WriteLine();
    }

    private void WriteEnumMembers(WindowsAccessBridgeModel model, SourceCodeWriter sourceWriter, EnumDefinition definition) {
      sourceWriter.IncIndent();
      definition.Members.ForEach(x => {
        sourceWriter.WriteIndent();
        sourceWriter.Write("{0}", x.Name);
        if (!string.IsNullOrEmpty(x.Value)) {
          sourceWriter.Write(" = {0}", x.Value);
        }
        sourceWriter.Write(",");
        sourceWriter.WriteLine();
      });
      sourceWriter.DecIndent();
    }

   private static string ToPascalCase(EventDefinition definition) {
      return char.ToLower(definition.Name[0]) + definition.Name.Substring(1);
    }
  }
}