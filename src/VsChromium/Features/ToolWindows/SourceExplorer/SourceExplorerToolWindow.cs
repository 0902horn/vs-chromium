﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Core.Logging;
using VsChromium.Features.AutoUpdate;
using VsChromium.Wpf;
using Constants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  /// <summary>
  /// This class implements the tool window exposed by this package and hosts a user control.
  ///
  /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane, 
  /// usually implemented by the package implementer.
  ///
  /// This class derives from the ToolWindowPane class provided from the MPF in order to use its 
  /// implementation of the IVsUIElementPane interface.
  /// </summary>
  [Guid(GuidList.GuidSourceExplorerToolWindowString)]
  public class SourceExplorerToolWindow : ToolWindowPane, IOleCommandTarget {
    private VsWindowFrameNotifyHandler _frameNotify;
    private OleCommandTarget _oleCommandTarget;

    /// <summary>
    /// Standard constructor for the tool window.
    /// </summary>
    public SourceExplorerToolWindow()
      : base(null) {
      // Set the window title reading it from the resources.
      Caption = Resources.SourceExplorerToolWindowTitle;
      // Set the image that will appear on the tab of the window frame
      // when docked with an other window
      // The resource ID correspond to the one defined in the resx file
      // while the Index is the offset in the bitmap strip. Each image in
      // the strip being 16x16.
      BitmapResourceID = 301;
      BitmapIndex = 1;

      // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
      // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
      // the object returned by the Content property.
      ExplorerControl = new SourceExplorerControl();
    }

    public override void OnToolWindowCreated() {
      base.OnToolWindowCreated();
      ExplorerControl.OnToolWindowCreated(this);

      // Advise IVsWindowFrameNotify so we know when we get hidden, etc.
      var frame = this.Frame as IVsWindowFrame2;
      if (frame != null) {
        _frameNotify = new VsWindowFrameNotifyHandler(frame);
        _frameNotify.Advise();
      }

      // Hookup command handlers
      var commands = new AggregateCommandTarget(new List<ICommandTarget>  {
        new PreviousLocationCommandHandler(this),
        new NextLocationCommandHandler(this),
        // Add more here...
      });

      _oleCommandTarget = new OleCommandTarget(commands);
    }

    public SourceExplorerControl ExplorerControl {
      get { return Content as SourceExplorerControl; }
      set { Content = value; }
    }

    public bool IsVisible {
      get {
        return _frameNotify != null &&
               _frameNotify.IsVisible;
      }
    }

    public void FocusSearchTextBox(CommandID commandId) {
      switch (commandId.ID) {
        case PkgCmdIdList.CmdidSearchFileNames:
          ExplorerControl.FileNamesSearch.Focus();
          break;
        case PkgCmdIdList.CmdidSearchDirectoryNames:
          ExplorerControl.DirectoryNamesSearch.Focus();
          break;
        case PkgCmdIdList.CmdidSearchText:
          ExplorerControl.FileContentsSearch.Focus();
          break;
      }
    }

    enum Direction {
      Next,
      Previous
    }

    private T GetNextLocationEntry<T>(Direction direction) where T : class, IHierarchyObject {
      if (ExplorerControl.ViewModel.ActiveDisplay != SourceExplorerViewModel.DisplayKind.TextSearchResult)
        return null;

      var item = ExplorerControl.FileTreeView.SelectedItem;
      if (item == null) {
        if (ExplorerControl.ViewModel.ActiveRootNodes == null)
          return null;

        if (ExplorerControl.ViewModel.ActiveRootNodes.Count == 0)
          return null;

        item = ExplorerControl.ViewModel.ActiveRootNodes[0].ParentViewModel;
        if (item == null)
          return null;
      }

      var nextItem = (direction == Direction.Next)
        ? new HierarchyObjectNavigator().GetNextItemOfType<T>(item as IHierarchyObject)
        : new HierarchyObjectNavigator().GetPreviousItemOfType<T>(item as IHierarchyObject);

      return nextItem;
    }

    public bool HasNextLocation() {
      return GetNextLocationEntry<FilePositionViewModel>(Direction.Next) != null;
    }

    public bool HasPreviousLocation() {
      return GetNextLocationEntry<FilePositionViewModel>(Direction.Previous) != null;
    }

    public void NavigateToNextLocation() {
      var nextItem = GetNextLocationEntry<FilePositionViewModel>(Direction.Next);
      NavigateToTreeViewItem(nextItem);
    }

    public void NavigateToPreviousLocation() {
      var previousItem = GetNextLocationEntry<FilePositionViewModel>(Direction.Previous);
      NavigateToTreeViewItem(previousItem);
    }

    private void NavigateToTreeViewItem(TreeViewItemViewModel item) {
      if (item == null)
        return;
      ExplorerControl.Controller.BringItemViewModelToView(item);
      ExplorerControl.Controller.ExecutedOpenCommandForItem(item);
    }

    public void NotifyPackageUpdate(UpdateInfo updateInfo) {
      WpfUtilities.Post(ExplorerControl, () => {
        ExplorerControl.UpdateInfo = updateInfo;
      });
    }

    int IOleCommandTarget.QueryStatus(ref System.Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, System.IntPtr pCmdText) {
      var hr = (int) Constants.OLECMDERR_E_NOTSUPPORTED;
      if (_oleCommandTarget != null) {
        hr = _oleCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
      }
      return hr;
    }

    int IOleCommandTarget.Exec(ref System.Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, System.IntPtr pvaIn, System.IntPtr pvaOut) {
      var hr = (int)Constants.OLECMDERR_E_NOTSUPPORTED;
      if (_oleCommandTarget != null) {
        hr = _oleCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      }
      return hr;
    }
  }
}
