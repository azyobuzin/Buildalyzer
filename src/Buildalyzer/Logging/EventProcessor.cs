﻿using Buildalyzer.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Buildalyzer.Logging
{
    internal class EventProcessor
    {
        private readonly string _projectFilePath;
        private readonly Microsoft.Build.Logging.StructuredLogger.Construction _construction;

        public EventProcessor(string projectFilePath, bool analyze)
        {
            _projectFilePath = projectFilePath;
            if(analyze)
            {
                _construction = new Microsoft.Build.Logging.StructuredLogger.Construction();
            }
        }

        public void Initialize(IEventSource eventSource, IEnumerable<ILogger> loggers)
        {
            // Initialize the loggers
            foreach(ILogger logger in loggers)
            {
                logger.Initialize(eventSource);
            }

            // Send events to the tree constructor
            if (_construction != null)
            {
                eventSource.BuildStarted += _construction.BuildStarted;
                eventSource.BuildFinished += _construction.BuildFinished;
                eventSource.ProjectStarted += _construction.ProjectStarted;
                eventSource.ProjectFinished += _construction.ProjectFinished;
                eventSource.TargetStarted += _construction.TargetStarted;
                eventSource.TargetFinished += _construction.TargetFinished;
                eventSource.TaskStarted += _construction.TaskStarted;
                eventSource.TaskFinished += _construction.TaskFinished;
                eventSource.MessageRaised += _construction.MessageRaised;
                eventSource.WarningRaised += _construction.WarningRaised;
                eventSource.ErrorRaised += _construction.ErrorRaised;
                eventSource.CustomEventRaised += _construction.CustomEventRaised;
                eventSource.StatusEventRaised += _construction.StatusEventRaised;
            }
        }

        public IEnumerable<AnalyzerResult> GetResults()
        {
            if (_construction != null)
            {
                // Group all nested projects by TFM under a single tree
                // We want all project nodes since we don't know exactly which targets are being built
                Dictionary<string, TreeNode> projects = new Dictionary<string, TreeNode>();
                _construction.Build.VisitAllChildren<Project>(x => ProjectVisitor(x, projects));
            }

            return Array.Empty<AnalyzerResult>();
        }

        private void ProjectVisitor(Project project, Dictionary<string, TreeNode> projects)
        {
            // Make sure this is the same project, nested MSBuild tasks may have spawned additional builds of other projects
            if (ProjectFile.NormalizePath(project.ProjectFile) != _projectFilePath)
            {
                return;
            }

            // Get the TFM for this project
            string tfm = project
                .FindChild<Folder>("Properties")
                ?.FindChild<NameValueNode>(x => string.Equals(x.Name, "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(tfm))
            {
                // Add this project to the tree for this TFM
                TreeNode tree = null;
                if (!projects.TryGetValue(tfm, out tree))
                {
                    tree = new NamedNode();
                    projects.Add(tfm, tree);
                }
                tree.AddChild(project);
            }
        }
    }
}