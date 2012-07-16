﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using AnotherJiraRestClient.JiraModel;
using RestSharp;

namespace AnotherJiraRestClient
{
    // TODO: Exception handling. When Jira service is unavailible, when response code is
    // unexpected, etc.

    // TODO: Check if response.ResponseStatus == ResponseStatus.Error

    // TODO: What if URL is too long?

    // TODO: Add PUT /rest/api/2/application-properties/{id}


    /// <summary>
    /// Class used for all interaction with the Jira API. See 
    /// http://docs.atlassian.com/jira/REST/latest/ for documentation of the
    /// Jira API.
    /// </summary>
    public class JiraClient
    {
        private readonly RestClient client;

        /// <summary>
        /// Constructs a JiraClient. Please note, the baseUrl from the account
        /// needs to be https (not http), otherwise Jira will response with 
        /// unauthorized.
        /// </summary>
        /// <param name="account">Jira account information</param>
        public JiraClient(JiraAccount account)
        {
            client = new RestClient(account.ServerUrl)
            {
                Authenticator = new HttpBasicAuthenticator(account.User, account.Password)
            };
        }

        /// <summary>
        /// Executes a RestRequest and returns the deserialized response. If
        /// the response hasn't got the specified response code or if an
        /// exception was thrown during execution a JiraApiException will be 
        /// thrown.
        /// </summary>
        /// <typeparam name="T">Request return type</typeparam>
        /// <param name="request">request to execute</param>
        /// <returns>deserialized response of request</returns>
        public T Execute<T>(RestRequest request, HttpStatusCode expectedResponseCode) where T : new()
        {
            var response = client.Execute<T>(request);

            if (response.ResponseStatus != ResponseStatus.Completed || response.ErrorException != null)
                throw new JiraApiException(
                      "RestSharp response status: " + response.ResponseStatus + " - HTTP response: " + response.StatusCode + " - " + response.StatusDescription
                    + " - " + response.Content);
            else
                return response.Data;
        }

        /// <summary>
        /// Returns a comma separated string from the strings in the provided
        /// IEnumerable of strings.
        /// </summary>
        /// <param name="strings">a comma separated string based on the pro</param>
        /// <returns></returns>
        private static string ToCommaSeparatedString(IEnumerable<string> strings)
        {
            if (strings != null)
                return string.Join(",", strings);
            else
                return string.Empty;
        }

        /// <summary>
        /// Returns the Issue with the specified key. If the fields parameter
        /// is specified only the given field names will be loaded. Issue
        /// contains the availible field names, for example Issue.SUMMARY.
        /// </summary>
        /// <param name="issueKey">Issue key</param>
        /// <param name="fields">Fields to load</param>
        /// <returns>The issue with the specified key</returns>
        public Issue GetIssue(string issueKey, IEnumerable<string> fields = null)
        {
            var fieldsString = ToCommaSeparatedString(fields);
            
            var request = new RestRequest();
            // TODO: Move /rest/api/2 elsewhere
            request.Resource = "/rest/api/2/issue/" + issueKey + "?fields=" + fieldsString;
            request.Method = Method.GET;
            return Execute<Issue>(request, HttpStatusCode.OK);
        }

        /// <summary>
        /// Searches for Issues using JQL.
        /// </summary>
        /// <param name="jql">a JQL search string</param>
        /// <returns>searchresults</returns>
        public Issues GetIssuesByJql(string jql, int startAt, int maxResults, IEnumerable<string> fields = null)
        {
            var request = new RestRequest();
            request.Resource = "/rest/api/2/search";
            request.AddParameter(new Parameter()
                {
                    Name = "jql",
                    Value = jql,
                    Type = ParameterType.GetOrPost
                });
            request.AddParameter(new Parameter()
            {
                Name = "fields",
                Value = ToCommaSeparatedString(fields),
                Type = ParameterType.GetOrPost
            });
            request.AddParameter(new Parameter()
            {
                Name = "startAt",
                Value = startAt,
                Type = ParameterType.GetOrPost
            });
            request.AddParameter(new Parameter()
            {
                Name = "maxResults",
                Value = maxResults,
                Type = ParameterType.GetOrPost
            });
            request.Method = Method.GET;
            return Execute<Issues>(request, HttpStatusCode.OK);
        }

        /// <summary>
        /// Returns the Issues for the specified project.
        /// </summary>
        /// <param name="projectKey">project key</param>
        /// <returns>the Issues of the specified project</returns>
        public Issues GetIssuesByProject(string projectKey, int startAt, int maxResults, IEnumerable<string> fields = null)
        {
            return GetIssuesByJql("project=" + projectKey, startAt, maxResults, fields);
        }

        /// <summary>
        /// Returns a list of all possible priorities.
        /// </summary>
        /// <returns></returns>
        public List<Priority> GetPriorities()
        {
            var request = new RestRequest();
            // TODO: Move /rest/api/2 elsewhere
            request.Resource = "/rest/api/2/priority";
            request.Method = Method.GET;
            return Execute<List<Priority>>(request, HttpStatusCode.OK);
        }

        /// <summary>
        /// Returns the meta data for creating issues. This includes the 
        /// available projects and issue types, but not fields (fields
        /// are supported in the Jira api but not by this wrapper).
        /// </summary>
        /// <param name="projectKey"></param>
        /// <returns>the meta data for creating issues</returns>
        public ProjectMeta GetProjectMeta(string projectKey)
        {
            var request = new RestRequest();
            request.Resource = "/rest/api/2/issue/createmeta";
            request.AddParameter(new Parameter() 
              { Name = "projectKeys", 
                Value = projectKey, 
                Type = ParameterType.GetOrPost });
            request.Method = Method.GET;
            var createMeta = Execute<IssueCreateMeta>(request, HttpStatusCode.OK);
            if (createMeta.projects[0].key != projectKey || createMeta.projects.Count != 1)
                // TODO: Error message
                throw new JiraApiException();
            return createMeta.projects[0];
        }

        /// <summary>
        /// Returns a list of all possible statuses.
        /// </summary>
        /// <returns></returns>
        public List<Status> GetStatuses()
        {
            var request = new RestRequest();
            // TODO: Move /rest/api/2 elsewhere
            request.Resource = "/rest/api/2/status";
            request.Method = Method.GET;
            return Execute<List<Status>>(request, HttpStatusCode.OK);
        }

        /// <summary>
        /// Creates a new issue.
        /// </summary>
        /// <param name="projectKey"></param>
        /// <param name="summary"></param>
        /// <param name="description"></param>
        /// <param name="issueTypeId"></param>
        /// <param name="priorityId"></param>
        /// <param name="labels"></param>
        /// <returns>the new issue</returns>
        public BasicIssue CreateIssue(string projectKey, string summary, string description, string issueTypeId, string priorityId, IEnumerable<string> labels)
        {
            // TODO: Can you add custom fields by using an ExpandoObject??
            var request = new RestRequest()
            {
                Resource = "rest/api/2/issue",
                RequestFormat = DataFormat.Json,
                Method = Method.POST
            };

            request.AddBody(new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = summary,
                    description = description,
                    issuetype = new { id = issueTypeId },
                    priority = new { id = priorityId },
                    labels = labels
                }
            });

            return Execute<BasicIssue>(request, HttpStatusCode.Created);
        }

        public ApplicationProperty GetApplicationProperty(string propertyKey)
        {
            var request = new RestRequest()
            {
                Method = Method.GET,
                Resource = "rest/api/2/application-properties",
                RequestFormat = DataFormat.Json
            };
            
            request.AddParameter(new Parameter()
            {
                Name = "key",
                Value = propertyKey,
                Type = ParameterType.GetOrPost
            });

            return Execute<ApplicationProperty>(request, HttpStatusCode.OK);
        }

        public Attachment GetAttachment(string attachmentId)
        {
            var request = new RestRequest()
            {
                Method = Method.GET,
                Resource = "rest/api/2/attachment/" + attachmentId,
                RequestFormat = DataFormat.Json
            };

            return Execute<Attachment>(request, HttpStatusCode.OK);
        }

        public void DeleteAttachment(string attachmentId)
        {
            var request = new RestRequest()
            {
                Method = Method.DELETE,
                Resource = "rest/api/2/attachment/" + attachmentId
            };

            var response = client.Execute(request);
            if (response.ResponseStatus != ResponseStatus.Completed || response.StatusCode != HttpStatusCode.NoContent)
                throw new JiraApiException("Failed to delete attachment with id=" + attachmentId);
        }
    }
}
