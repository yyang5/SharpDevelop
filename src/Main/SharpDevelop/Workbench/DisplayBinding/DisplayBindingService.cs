﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.Core;

namespace ICSharpCode.SharpDevelop.Workbench
{
	/// <summary>
	/// This class handles the installed display bindings
	/// and provides a simple access point to these bindings.
	/// </summary>
	sealed class DisplayBindingService : IDisplayBindingService
	{
		const string displayBindingPath = "/SharpDevelop/Workbench/DisplayBindings";
		
		Properties displayBindingServiceProperties;
		
		List<DisplayBindingDescriptor> bindings;
		List<ExternalProcessDisplayBinding> externalProcessDisplayBindings = new List<ExternalProcessDisplayBinding>();
		
		public DisplayBindingService()
		{
			bindings = AddInTree.BuildItems<DisplayBindingDescriptor>(displayBindingPath, null, true);
			displayBindingServiceProperties = PropertyService.NestedProperties("DisplayBindingService");
			foreach (var binding in displayBindingServiceProperties.GetList<ExternalProcessDisplayBinding>("ExternalProcesses")) {
				if (binding != null) {
					AddExternalProcessDisplayBindingInternal(binding);
				}
			}
		}
		
		public DisplayBindingDescriptor AddExternalProcessDisplayBinding(ExternalProcessDisplayBinding binding)
		{
			SD.MainThread.VerifyAccess();
			if (binding == null)
				throw new ArgumentNullException("binding");
			DisplayBindingDescriptor descriptor = AddExternalProcessDisplayBindingInternal(binding);
			SaveExternalProcessDisplayBindings();
			return descriptor;
		}
		
		void SaveExternalProcessDisplayBindings()
		{
			displayBindingServiceProperties.SetList("ExternalProcesses", externalProcessDisplayBindings);
		}
		
		DisplayBindingDescriptor AddExternalProcessDisplayBindingInternal(ExternalProcessDisplayBinding binding)
		{
			externalProcessDisplayBindings.Add(binding);
			DisplayBindingDescriptor descriptor = new DisplayBindingDescriptor(binding) {
				Id = binding.Id,
				Title = binding.Title
			};
			bindings.Add(descriptor);
			return descriptor;
		}
		
		public void RemoveExternalProcessDisplayBinding(ExternalProcessDisplayBinding binding)
		{
			SD.MainThread.VerifyAccess();
			if (binding == null)
				throw new ArgumentNullException("binding");
			if (!externalProcessDisplayBindings.Remove(binding))
				throw new ArgumentException("binding was not added");
			SaveExternalProcessDisplayBindings();
			for (int i = 0; i < bindings.Count; i++) {
				if (bindings[i].GetLoadedBinding() == binding) {
					bindings.RemoveAt(i);
					return;
				}
			}
			throw new InvalidOperationException("did not find binding descriptor even though binding was registered");
		}
		
		/// <summary>
		/// Gets the primary display binding for the specified file name.
		/// </summary>
		public IDisplayBinding GetBindingPerFileName(FileName filename)
		{
			SD.MainThread.VerifyAccess();
			if (FileUtility.IsUrl(filename)) {
				// The normal display binding dispatching code can't handle URLs (e.g. because it uses Path.GetExtension),
				// so we'll directly return the browser display binding.
				return new BrowserDisplayBinding.BrowserDisplayBinding();
			}
			DisplayBindingDescriptor codon = GetDefaultCodonPerFileName(filename);
			return codon == null ? null : codon.Binding;
		}
		
		/// <summary>
		/// Gets the default primary display binding for the specified file name.
		/// </summary>
		public DisplayBindingDescriptor GetDefaultCodonPerFileName(FileName filename)
		{
			SD.MainThread.VerifyAccess();
			
			string defaultCommandID = displayBindingServiceProperties.Get("Default" + Path.GetExtension(filename).ToLowerInvariant(), string.Empty);
			if (!string.IsNullOrEmpty(defaultCommandID)) {
				foreach (DisplayBindingDescriptor binding in bindings) {
					if (binding.Id == defaultCommandID) {
						if (IsPrimaryBindingValidForFileName(binding, filename)) {
							return binding;
						}
					}
				}
			}
			DisplayBindingDescriptor autoDetectDescriptor = null;
			foreach (DisplayBindingDescriptor binding in bindings) {
				if (IsPrimaryBindingValidForFileName(binding, filename)) {
					if (binding.Binding.IsPreferredBindingForFile(filename))
						return binding;
					else if (binding.Binding is AutoDetectDisplayBinding)
						autoDetectDescriptor = binding;
				}
			}
			return autoDetectDescriptor;
		}
		
		public void SetDefaultCodon(string extension, DisplayBindingDescriptor bindingDescriptor)
		{
			SD.MainThread.VerifyAccess();
			if (bindingDescriptor == null)
				throw new ArgumentNullException("bindingDescriptor");
			if (extension == null)
				throw new ArgumentNullException("extension");
			if (!extension.StartsWith(".", StringComparison.Ordinal))
				throw new ArgumentException("extension must start with '.'");
			
			displayBindingServiceProperties.Set("Default" + extension.ToLowerInvariant(), bindingDescriptor.Id);
		}
		
		/// <summary>
		/// Gets list of possible primary display bindings for the specified file name.
		/// </summary>
		public IReadOnlyList<DisplayBindingDescriptor> GetCodonsPerFileName(FileName filename)
		{
			SD.MainThread.VerifyAccess();
			
			List<DisplayBindingDescriptor> list = new List<DisplayBindingDescriptor>();
			foreach (DisplayBindingDescriptor binding in bindings) {
				if (IsPrimaryBindingValidForFileName(binding, filename)) {
					list.Add(binding);
				}
			}
			return list;
		}
		
		static bool IsPrimaryBindingValidForFileName(DisplayBindingDescriptor binding, FileName filename)
		{
			if (!binding.IsSecondary && binding.CanOpenFile(filename)) {
				if (binding.Binding != null && binding.Binding.CanCreateContentForFile(filename)) {
					return true;
				}
			}
			return false;
		}
		
		/// <summary>
		/// Attach secondary view contents to the view content.
		/// </summary>
		/// <param name="viewContent">The view content to attach to</param>
		/// <param name="isReattaching">This is a reattaching pass</param>
		public void AttachSubWindows(IViewContent viewContent, bool isReattaching)
		{
			SD.MainThread.VerifyAccess();
			if (viewContent == null)
				throw new ArgumentNullException("viewContent");
			
			foreach (DisplayBindingDescriptor binding in bindings) {
				if (binding.IsSecondary && binding.CanOpenFile(viewContent.PrimaryFileName)) {
					ISecondaryDisplayBinding displayBinding = binding.SecondaryBinding;
					if (displayBinding != null
					    && (!isReattaching || displayBinding.ReattachWhenParserServiceIsReady)
					    && displayBinding.CanAttachTo(viewContent))
					{
						IViewContent[] subViewContents = binding.SecondaryBinding.CreateSecondaryViewContent(viewContent);
						if (subViewContents != null) {
							Array.ForEach(subViewContents, viewContent.SecondaryViewContents.Add);
						} else {
							MessageService.ShowError("Can't attach secondary view content. " + binding.SecondaryBinding + " returned null for " + viewContent + ".\n(should never happen)");
						}
					}
				}
			}
		}
	}
}