# consolidate_temporary_results

Consolidate temporary job results from a scraping session to the final MongoDB collection. Call this after a scraping operation completes or is interrupted to save the results permanently.

## When to Use

- After any scraping operation completes successfully
- When recovering from an interrupted or cancelled scraping operation
- When an AI timeout occurs during a long-running scrape
- To move auto-saved temporary results to permanent storage

## How It Works

All scraping operations (bulk, single-site, multi-site) automatically save results to a temporary collection (`search_results_temp`) as batches complete. This provides resilience against crashes, timeouts, and cancellations. Use this tool to move those temporary results to the final collection (`search_results`) with deduplication.

## Parameters

- **sessionId** (string): Session identifier from the scraping operation
  - Returned by scraping tools in their response
  - Groups all results from a single scraping session
  - Example: `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`
- **userId** (string): User identifier for ownership and organization
  - Jobs will be tagged with this userId
  - Example: `"bulk_user"` or `"john_doe"`

## Returns

JSON string containing:
- **success** (boolean): Whether consolidation completed successfully
- **sessionId** (string): The session ID that was consolidated
- **jobsConsolidated** (number): Total number of jobs found in temporary storage
- **jobsSaved** (number): Number of jobs saved to final collection (after deduplication)
- **message** (string): Status message describing the result

## Example

```json
{
  "success": true,
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "jobsConsolidated": 47,
  "jobsSaved": 42,
  "message": "Successfully consolidated 47 jobs from temporary storage"
}
```

## Workflow Example

```
1. Start bulk job → returns sessionId
2. Job runs for 5 minutes, auto-saving each batch to temp collection
3. AI times out or user cancels
4. Call get_temporary_results(sessionId) to inspect what was saved
5. Call consolidate_temporary_results(sessionId, userId) to save to final collection
6. Jobs now permanently stored and can be retrieved via get_stored_jobs
```

## Notes

- Temporary results are automatically saved during scraping - you don't need to do anything special
- The consolidation process performs deduplication (same as save_jobs)
- Temporary results are marked as consolidated after processing (not deleted)
- Old temporary results (>24 hours or already consolidated) are cleaned up automatically
- If no temporary results exist for the sessionId, consolidation returns success with 0 jobs
