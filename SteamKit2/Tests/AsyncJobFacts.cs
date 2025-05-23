﻿using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using Xunit;

namespace Tests
{
    public class AsyncJobFacts
    {
        class Callback : CallbackMsg
        {
            public bool IsFinished { get; set; }
        }

        [Fact]
        public void AsyncJobFailureWhenClientDiconnected()
        {
            SteamClient client = new SteamClient();
            
            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            Task<Callback> jobTask = asyncJob.ToTask();
            
            Assert.True( jobTask.IsCompleted, "Async job should be completed when client is disconnected" );
            Assert.False( jobTask.IsCanceled, "Async job should not be when client is disconnected" );
            Assert.True( jobTask.IsFaulted, "Async job should be faulted when client is disconnected" );
        }

#if DEBUG
         [Fact]
        public void AysncJobCompletesOnCallback()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            Task<Callback> asyncTask = asyncJob.ToTask();

            client.PostCallback( new Callback { JobID = 123 } );

            Assert.True( asyncTask.IsCompleted, "Async job should be completed after callback is posted" );
            Assert.False( asyncTask.IsCanceled, "Async job should not be canceled after callback is posted" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted after callback is posted" );
        }

        [Fact]
        public async Task AsyncJobGivesBackCallback()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            Task<Callback> jobTask = asyncJob.ToTask();

            Callback ourCallback = new Callback { JobID = 123 };

            client.PostCallback( ourCallback );

            Assert.Same( await jobTask, ourCallback );
        }

        [Fact]
        public void AsyncJobCtorRegistersJob()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );

            Assert.True( client.jobManager.asyncJobs.ContainsKey( asyncJob ), "Async job dictionary should contain the jobid key" );
            Assert.True( client.jobManager.asyncJobs.ContainsKey( 123 ), "Async job dictionary should contain jobid key as a value type" );
        }

        [Fact]
        public void AsyncJobClearsOnCompletion()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );

            client.PostCallback( new Callback { JobID = 123 } );

            Assert.False( client.jobManager.asyncJobs.ContainsKey( asyncJob ), "Async job dictionary should no longer contain jobid key after callback is posted" );
            Assert.False( client.jobManager.asyncJobs.ContainsKey( 123 ), "Async job dictionary should no longer contain jobid key (as value type) after callback is posted" );
        }

        [Fact]
        public async Task AsyncJobClearsOnTimeout()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );
            client.jobManager.CancelTimedoutJobs();

            Assert.False( client.jobManager.asyncJobs.ContainsKey( asyncJob ), "Async job dictionary should no longer contain jobid key after timeout" );
            Assert.False( client.jobManager.asyncJobs.ContainsKey( 123 ), "Async job dictionary should no longer contain jobid key (as value type) after timeout" );
        }

        [Fact]
        public async Task AsyncJobCancelsOnSetFailedTimeout()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            Task<Callback> asyncTask = asyncJob.ToTask();

            asyncJob.SetFailed( dueToRemoteFailure: false );
            
            Assert.True( asyncTask.IsCompleted, "Async job should be completed on message timeout" );
            Assert.True( asyncTask.IsCanceled, "Async job should be canceled on message timeout" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted on message timeout" );

            await Assert.ThrowsAsync<TaskCanceledException>( async () => await asyncTask );
        }

        [Fact]
        public async Task AsyncJobTimesout()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            Task<Callback> asyncTask = asyncJob.ToTask();

            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );
            client.jobManager.CancelTimedoutJobs();

            Assert.True( asyncTask.IsCompleted, "Async job should be completed yet" );
            Assert.True( asyncTask.IsCanceled, "Async job should be canceled yet" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted yet" );

            await Assert.ThrowsAsync<TaskCanceledException>( async () => await asyncTask );
        }

        [Fact]
        public void AsyncJobThrowsExceptionOnNullCallback()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );

            Assert.Throws<ArgumentNullException>( () => asyncJob.AddResult( null ) );
        }

        [Fact]
        public async Task AsyncJobThrowsFailureExceptionOnFailure()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJob<Callback> asyncJob = new AsyncJob<Callback>( client, 123 );
            Task<Callback> asyncTask = asyncJob.ToTask();

            asyncJob.SetFailed( dueToRemoteFailure: true );

            Assert.True( asyncTask.IsCompleted, "Async job should be completed after job failure" );
            Assert.False( asyncTask.IsCanceled, "Async job should not be canceled after job failure" );
            Assert.True( asyncTask.IsFaulted, "Async job should be faulted after job failure" );

            await Assert.ThrowsAsync<AsyncJobFailedException>( async () => await asyncTask );
        }

        [Fact]
        public void AsyncJobMultipleFinishedOnEmptyPredicate()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => true );
            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            bool jobFinished = asyncJob.AddResult( new Callback { JobID = 123 } );

            Assert.True( jobFinished, "Async job should inform that it is completed when completion predicate is always true and a result is given" );
            Assert.True( asyncTask.IsCompleted, "Async job should be completed when empty predicate result is given" );
            Assert.False( asyncTask.IsCanceled, "Async job should not be canceled when empty predicate result is given" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted when empty predicate result is given" );
        }

        [Fact]
        public void AsyncJobMultipleFinishedOnPredicate()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => call.IsFinished );
            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            bool jobFinished = asyncJob.AddResult( new Callback { JobID = 123, IsFinished = false } );

            Assert.False( jobFinished, "Async job should not inform that it is finished when completion predicate is false after a result is given" );
            Assert.False( asyncTask.IsCompleted, "Async job should not be completed when completion predicate is false" );
            Assert.False( asyncTask.IsCanceled, "Async job should not be canceled when completion predicate is false" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted when completion predicate is false" );

            jobFinished = asyncJob.AddResult( new Callback { JobID = 123, IsFinished = true } );

            Assert.True( jobFinished, "Async job should inform completion when completion predicat is passed after a result is given" );
            Assert.True( asyncTask.IsCompleted, "Async job should be completed when completion predicate is true" );
            Assert.False( asyncTask.IsCanceled, "Async job should not be canceled when completion predicate is true" );
            Assert.False( asyncTask.IsFaulted, "Async job should not be faulted when completion predicate is true" );
        }

        [Fact]
        public void AsyncJobMultipleClearsOnCompletion()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => call.IsFinished );

            client.PostCallback( new Callback { JobID = 123, IsFinished = true } );

            Assert.False( client.jobManager.asyncJobs.ContainsKey( asyncJob ), "Async job dictionary should not contain jobid key for AsyncJobMultiple on completion" );
            Assert.False( client.jobManager.asyncJobs.ContainsKey( 123 ), "Async job dictionary should not contain jobid key (as value type) for AsyncJobMultiple on completion" );
        }

        [Fact]
        public async Task AsyncJobMultipleClearsOnTimeout()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, ccall => true );
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );
            client.jobManager.CancelTimedoutJobs();

            Assert.False( client.jobManager.asyncJobs.ContainsKey( asyncJob ), "Async job dictionary should no longer contain jobid key after timeout" );
            Assert.False( client.jobManager.asyncJobs.ContainsKey( 123 ), "Async job dictionary should no longer contain jobid key (as value type) after timeout" );
        }

        [Fact]
        public async Task AsyncJobMultipleExtendsTimeoutOnMessage()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => call.IsFinished );
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            // we should not be completed or canceled yet
            Assert.False( asyncTask.IsCompleted, "AsyncJobMultiple should not be completed yet" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled yet" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted yet" );

            // give result 1 of 2
            asyncJob.AddResult( new Callback { JobID = 123, IsFinished = false } );

            // delay for what the original timeout would have been
            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );

            client.jobManager.CancelTimedoutJobs();

            // we still shouldn't be completed or canceled (timed out)
            Assert.False( asyncTask.IsCompleted, "AsyncJobMultiple should not be completed yet after result was added (result should extend timeout)" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled yet after result was added (result should extend timeout)" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted yet after a result was added (result should extend timeout)" );

            asyncJob.AddResult( new Callback { JobID = 123, IsFinished = true } );

            // we should be completed but not canceled or faulted
            Assert.True( asyncTask.IsCompleted, "AsyncJobMultiple should be completed when final result is added to set" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled when final result is added to set" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted when final result is added to set" );
        }

        [Fact]
        public async Task AsyncJobMultipleTimesout()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => false );
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );
            client.jobManager.CancelTimedoutJobs();

            Assert.True( asyncTask.IsCompleted, "AsyncJobMultiple should be completed after job timeout" );
            Assert.True( asyncTask.IsCanceled, "AsyncJobMultiple should be canceled after job timeout" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted after job timeout" );

            await Assert.ThrowsAsync<TaskCanceledException>( async () => await asyncTask );
        }

        [Fact]
        public async Task AsyncJobMultipleCompletesOnIncompleteResult()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => call.IsFinished );
            asyncJob.Timeout = TimeSpan.FromSeconds( 1 );

            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            Callback onlyResult = new Callback { JobID = 123, IsFinished = false };

            asyncJob.AddResult( onlyResult );

            // adding a result will extend the job's timeout, but we'll cheat here and decrease it
            asyncJob.Timeout = TimeSpan.FromMilliseconds( 50 );

            await Task.Delay( TimeSpan.FromMilliseconds( 70 ), TestContext.Current.CancellationToken );
            client.jobManager.CancelTimedoutJobs();

            Assert.True( asyncTask.IsCompleted, "AsyncJobMultiple should be completed on partial (timed out) result set" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled on partial (timed out) result set" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted on a partial (failed) result set" );

            AsyncJobMultiple<Callback>.ResultSet result = await asyncTask;

            Assert.False( result.Complete, "ResultSet should be incomplete" );
            Assert.False( result.Failed, "ResultSet should not be failed" );
            Assert.Single( result.Results );
            Assert.Contains( onlyResult, result.Results );
        }

        [Fact]
        public async Task AsyncJobMultipleCompletesOnIncompleteResultAndFailure()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => call.IsFinished );
            asyncJob.Timeout = TimeSpan.FromSeconds( 1 );

            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            Callback onlyResult = new Callback { JobID = 123, IsFinished = false };

            asyncJob.AddResult( onlyResult );

            asyncJob.SetFailed( dueToRemoteFailure: true );

            Assert.True( asyncTask.IsCompleted, "AsyncJobMultiple should be completed on partial (failed) result set" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled on partial (failed) result set" );
            Assert.False( asyncTask.IsFaulted, "AsyncJobMultiple should not be faulted on a partial (failed) result set" );

            AsyncJobMultiple<Callback>.ResultSet result = await asyncTask;

            Assert.False( result.Complete, "ResultSet should be incomplete" );
            Assert.True( result.Failed, "ResultSet should be failed" );
            Assert.Single( result.Results );
            Assert.Contains( onlyResult, result.Results );
        }

        [Fact]
        public void AsyncJobMultipleThrowsExceptionOnNullCallback()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => true );

            Assert.Throws<ArgumentNullException>( () => asyncJob.AddResult( null ) );
        }

        [Fact]
        public async Task AsyncJobMultipleThrowsFailureExceptionOnFailure()
        {
            SteamClient client = ConnectedSteamClient.Get();

            AsyncJobMultiple<Callback> asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => false );
            Task<AsyncJobMultiple<Callback>.ResultSet> asyncTask = asyncJob.ToTask();

            asyncJob.SetFailed( dueToRemoteFailure: true );

            Assert.True( asyncTask.IsCompleted, "AsyncJobMultiple should be completed after job failure" );
            Assert.False( asyncTask.IsCanceled, "AsyncJobMultiple should not be canceled after job failure" );
            Assert.True( asyncTask.IsFaulted, "AsyncJobMultiple should be faulted after job failure" );

            await Assert.ThrowsAsync<AsyncJobFailedException>( async () => await asyncTask );
        }

        [Fact]
        public void AsyncJobContinuesAsynchronously()
        {
            SteamClient client = ConnectedSteamClient.Get();

            var asyncJob = new AsyncJob<Callback>( client, 123 );
            var asyncTask = asyncJob.ToTask();

            var continuationThreadID = -1;
            var continuation = asyncTask.ContinueWith( t =>
            {
                continuationThreadID = Environment.CurrentManagedThreadId;
            }, TaskContinuationOptions.ExecuteSynchronously );

            var completionThreadID = Environment.CurrentManagedThreadId;
            asyncJob.AddResult( new Callback { JobID = 123 } );

            WaitForTaskWithoutRunningInline( continuation );

            Assert.NotEqual( -1, continuationThreadID );
            Assert.NotEqual( completionThreadID, continuationThreadID );
        }

        [Fact]
        public void AsyncJobMultipleContinuesAsynchronously()
        {
            SteamClient client = ConnectedSteamClient.Get();

            var asyncJob = new AsyncJobMultiple<Callback>( client, 123, call => true );
            var asyncTask = asyncJob.ToTask();

            var continuationThreadID = -1;
            var continuation = asyncTask.ContinueWith( t =>
            {
                continuationThreadID = Environment.CurrentManagedThreadId;
            }, TaskContinuationOptions.ExecuteSynchronously );

            var completionThreadID = Environment.CurrentManagedThreadId;
            asyncJob.AddResult( new Callback { JobID = 123 } );

            WaitForTaskWithoutRunningInline( continuation );

            Assert.NotEqual( -1, continuationThreadID );
            Assert.NotEqual( completionThreadID, continuationThreadID );
        }

        static void WaitForTaskWithoutRunningInline( Task task )
        {
            // If we await the task, our Thread can go back to the scheduler and come eligible to
            // run task continuations. If we call .Wait with an infinite timeout / no cancellation token, then
            // the .NET runtime will attempt to run the task inline... on the current thread.
            // To avoid that we need to supply a cancellable-but-never-cancelled token, or do other hackery
            // with IAsyncResult or mutexes. This appears to be the simplest.
            using var cts = new CancellationTokenSource();
            task.Wait( cts.Token );
        }
#endif
    }
}
