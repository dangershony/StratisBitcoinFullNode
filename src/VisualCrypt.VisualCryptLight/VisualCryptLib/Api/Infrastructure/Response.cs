using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
	public class Response
	{
		const string Success = "SUCCESS";
		const string Canceled = "CANCELED";

		string _state;

		public bool IsSuccess
		{
			get
			{
				CheckStateHasBeenSet();
				return this._state == Success;
			}
		}

		public bool IsCanceled
		{
			get
			{
				CheckStateHasBeenSet();
				return this._state == Canceled;
			}
		}

		public string Error
		{
			get
			{
				CheckStateHasBeenSet();

				if (this._state == Success)
					throw new InvalidOperationException(
						"State of Response is '{0}'. Please check IsSuccess before accessing Response.Error unnecessarily."
							.FormatInvariant(this._state));

				return this._state;
			}
		}


		public void SetSuccess()
		{
			CheckNotSettingStateTwice();
			this._state = Success;
		}

		public void SetError(Exception e)
		{
			if(e == null)
				throw new ArgumentNullException("e");
			CheckNotSettingStateTwice();

			if (e is OperationCanceledException)
				this._state = Canceled;
			else
			{
				this._state = e.Message;
			}
		}

		public void SetError(string errorMessage)
		{
			CheckNotSettingStateTwice();

			if (string.IsNullOrWhiteSpace(errorMessage))
				throw new ArgumentNullException("errorMessage");

			this._state = errorMessage;
		}

		void CheckNotSettingStateTwice()
		{
			if (this._state != null)
				throw new InvalidOperationException("The state of the response must not be set more than one time.");
		}

		void CheckStateHasBeenSet()
		{
			if (this._state == null)
				throw new InvalidOperationException("The state of the response has not been set.");
		}

        public static void CheckSuccessOrThrow(params Response[] responses)
        {
            foreach (var response in responses)
            {
                if(response.IsSuccess)
                    continue;
                throw new Exception(response.Error);
            }
        }
    }

	public sealed class Response<T> : Response
	{
		public T Result { get; set; }
	}

	public sealed class Response<T, T2> : Response
	{
		public T Result { get; set; }
		public T2 Result2 { get; set; }
	}
}